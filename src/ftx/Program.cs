﻿using SecurityDriven.Inferno;
using SecurityDriven.Inferno.Extensions;
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Sockets;
using System.Security.Cryptography;

namespace ftx
{
    class Program
    {
        static void Main(string[] args)
        {
            var options = ProgramOptions.FromArgs(args);
            switch (options.ProgramMode)
            {
                case ProgramMode.Server:
                    RunServer(options);
                    break;
                case ProgramMode.Client:
                    RunClient(options);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private static void RunServer(ProgramOptions options)
        {
            var directoryPath = options.Directory.FullName;

            if (!new[] { '\\', '/' }.Contains(directoryPath.Last()))
                directoryPath += Path.DirectorySeparatorChar;

            var listener = new TcpListener(options.Host, options.Port);
            listener.Start();
            var display = new Display(options, listener.GetPort()) { Delay = TimeSpan.FromSeconds(5) };
            display.AttemptRefresh();

            try
            {
                var buffer = new byte[Extensions.DefaultStreamCopyBufferSize];

                using var client = listener.AcceptTcpClient();
                using var netStream = client.GetStream();
                using var compStream = options.Compression.HasValue
                    ? new DeflateStream(netStream, options.Compression.Value)
                    : default;
                using var clearReader = new BinaryReader(netStream);
                using var clearWriter = new BinaryWriter(clearReader.BaseStream);
                using var encryptor = options.Encrypt
                    ? CreateServerEncryptor(clearReader, clearWriter)
                    : default;
                using var cryptoStream = encryptor != null
                    ? new CryptoStream((Stream)compStream ?? netStream, encryptor, CryptoStreamMode.Write)
                    : null;
                using var writer = new BinaryWriter(cryptoStream ?? (Stream)compStream ?? netStream);

                display.Stopwatch.Start();

                foreach (var file in options.Directory.EnumerateFiles("*", new EnumerationOptions
                {
                    IgnoreInaccessible = true,
                    RecurseSubdirectories = true,
                }))
                {
                    var fileRelPath = file.FullName.Substring(directoryPath.Length);
                    display.CurrentFileProgress = new FileProgress(fileRelPath, file.Length);
                    display.CurrentFileProgress.Stopwatch.Start();

                    writer.Write(fileRelPath);
                    writer.Write(file.Length);

                    using (var fileStream = file.OpenRead())
                        fileStream.CopyTo(writer.BaseStream, file.Length, buffer, display.UpdateProgress);

                    display.FileCount++;
                    display.AttemptRefresh();
                }
            }
            finally
            {
                listener.Stop();
            }
        }

        private static void RunClient(ProgramOptions options)
        {
            var display = new Display(options, options.Port) { Delay = TimeSpan.FromSeconds(5) };

            using var client = new TcpClient();
            client.Connect(options.Host, options.Port);

            using var netStream = client.GetStream();
            using var compStream = options.Compression.HasValue
                ? new DeflateStream(netStream, CompressionMode.Decompress)
                : default;
            using var clearReader = new BinaryReader(netStream);
            using var clearWriter = new BinaryWriter(clearReader.BaseStream);
            using var decryptor = options.Encrypt
                ? CreateClientDecryptor(clearReader, clearWriter)
                : default;
            using var cryptoStream = decryptor != null
                ? new CryptoStream((Stream)compStream ?? netStream, decryptor, CryptoStreamMode.Read)
                : default;
            using var reader = new BinaryReader(cryptoStream ?? (Stream)compStream ?? netStream);
            display.Stopwatch.Start();

            var buffer = new byte[Extensions.DefaultStreamCopyBufferSize];

            while (true)
            {
                display.AttemptRefresh();

                string path;
                try
                {
                    path = reader.ReadString();
                }
                catch (EndOfStreamException)
                {
                    break;
                }

                var length = reader.ReadInt64();
                var file = options.Directory.GetFile(path);

                display.CurrentFileProgress = new FileProgress(path, length);
                display.CurrentFileProgress.Stopwatch.Start();

                var skipFile = file.Exists && !options.Overwrite;
                if (skipFile)
                {
                    reader.BaseStream.CopyTo(Stream.Null, length, buffer, display.UpdateProgress);
                }
                else
                {
                    if (!file.Directory.Exists)
                        file.Directory.Create();

                    using var fileStream = file.Open(FileMode.Create, FileAccess.Write, FileShare.None);
                    reader.BaseStream.CopyTo(fileStream, length, buffer, display.UpdateProgress);
                }

                display.FileCount++;
            }

        }

        private static ICryptoTransform CreateServerEncryptor(BinaryReader reader, BinaryWriter writer)
        {
            using var key = CngKeyExtensions.CreateNewDhmKey();

            // receive client's public key
            var remotePublicKey = reader.ReceivePublicKey();

            // send server's public key
            writer.SendPublicKey(key);

            // compute shared key
            var sharedKey = key.GetSharedDhmSecret(remotePublicKey);
            return new EtM_EncryptTransform(sharedKey);
        }

        private static ICryptoTransform CreateClientDecryptor(BinaryReader reader, BinaryWriter writer)
        {
            using var key = CngKeyExtensions.CreateNewDhmKey();

            // send client's public key
            writer.SendPublicKey(key);

            // receive server's public key
            var remotePublicKey = reader.ReceivePublicKey();

            // compute shared key
            var sharedKey = key.GetSharedDhmSecret(remotePublicKey);
            return new EtM_DecryptTransform(sharedKey);
        }
    }
}
