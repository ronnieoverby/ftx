using SecurityDriven.Inferno;
using System;
using System.IO;
using System.IO.Compression;
using System.Net.Sockets;
using System.Security.Cryptography;
using static ftx.Extensions;

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
            if (!Path.EndsInDirectorySeparator(directoryPath))
                directoryPath += Path.DirectorySeparatorChar;

            var listener = new TcpListener(options.Host, options.Port);
            listener.Start();
            var display = new Display(options, listener.GetPort());
            display.Refresh(observeDelay: false);

            try
            {
                var buffer = new byte[DefaultStreamCopyBufferSize];

                using var client = listener.AcceptTcpClient();
                using var netStream = client.GetStream();
                using var compStream = options.Compression.HasValue
                    ? new DeflateStream(netStream, options.Compression.Value)
                    : default;
                using var encryptor = options.Encrypt
                    ? new EtM_EncryptTransform(options.PSK)
                    : default;
                using var cryptoStream = encryptor != null
                    ? new CryptoStream((Stream)compStream ?? netStream, encryptor, CryptoStreamMode.Write)
                    : null;
                using var writer = new BinaryWriter(cryptoStream ?? (Stream)compStream ?? netStream);

                display.Stopwatch.Start();

                foreach (var file in options.Directory.EnumerateFiles("*", new EnumerationOptions
                {
                    IgnoreInaccessible = true,
                    RecurseSubdirectories = true
                }))
                {
                    var fileRelPath = file.FullName.Substring(directoryPath.Length);
                    display.CurrentFileProgress = new FileProgress(fileRelPath, file.Length);

                    writer.Write(fileRelPath);
                    writer.Write(file.Length);

                    using (var fileStream = file.OpenRead())
                        fileStream.CopyTo(writer.BaseStream, file.Length, buffer, display.UpdateFileProgress);

                    display.FileCount++;
                    display.Refresh();
                }

                display.Refresh(observeDelay: false);
            }
            finally
            {
                listener.Stop();
            }
        }

        private static void RunClient(ProgramOptions options)
        {
            var display = new Display(options, options.Port);
            display.Refresh(observeDelay: false);

            using var client = new TcpClient();
            client.Connect(options.Host, options.Port);

            using var netStream = client.GetStream();
            using var compStream = options.Compression.HasValue
                ? new DeflateStream(netStream, CompressionMode.Decompress)
                : default;
            using var decryptor = options.Encrypt
                ? new EtM_DecryptTransform(options.PSK)
                : default;
            using var cryptoStream = decryptor != null
                ? new CryptoStream((Stream)compStream ?? netStream, decryptor, CryptoStreamMode.Read)
                : default;
            using var reader = new BinaryReader(cryptoStream ?? (Stream)compStream ?? netStream);
            display.Stopwatch.Start();

            var buffer = new byte[DefaultStreamCopyBufferSize];

            while (decryptor?.IsComplete != true)
            {
                display.Refresh();

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

                var skipFile = file.Exists && !options.Overwrite;
                if (skipFile)
                {
                    reader.BaseStream.CopyTo(Stream.Null, length, buffer, display.UpdateFileProgress);
                }
                else
                {
                    if (!file.Directory.Exists)
                        file.Directory.Create();

                    using var fileStream = file.Open(FileMode.Create, FileAccess.Write, FileShare.None);
                    fileStream.SetLength(length);
                    reader.BaseStream.CopyTo(fileStream, length, buffer, display.UpdateFileProgress);
                }

                display.FileCount++;
            }

            display.Refresh(observeDelay: false);
        }
    }
}
