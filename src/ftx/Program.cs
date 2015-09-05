using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using CoreTechs.Common;

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
            var listener = new TcpListener(options.Host, options.Port);
            listener.Start();

            try
            {
                Console.WriteLine($"Listening on port {options.Port}.");

                using (var client = listener.AcceptTcpClient())
                using (var netStream = client.GetStream())
                using (var compStream = options.Compression.HasValue ? new DeflateStream(netStream, options.Compression.Value) : null)
                using (var aes = CreateAes(options))
                using (var enc = aes?.CreateEncryptor())
                using (var cryptoStream = aes != null ? new CryptoStream((Stream)compStream ?? netStream, enc, CryptoStreamMode.Write) : null)
                using (var writer = new BinaryWriter(cryptoStream ?? (Stream)compStream ?? netStream))
                {
                    Console.WriteLine("Connected.");

                    var files = options.Directory.EnumerateFiles("*", SearchOption.AllDirectories);
                    foreach (var file in files)
                    {
                        var path = file.GetRelativePathFrom(options.Directory);
                        Console.WriteLine(path);

                        FileStream fileStream;
                        try
                        {
                            fileStream = file.OpenRead();
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("Couldn't open file.");
                            Console.WriteLine(ex);
                            continue;
                        }

                        try
                        {
                            using (fileStream)
                            {
                                writer.Write(path);
                                writer.Write(file.Length);
                                fileStream.CopyTo(writer.BaseStream);
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(ex);
                            return;
                        }
                    }
                }
            }
            finally
            {
                listener.Stop();
            }
        }

        private static Aes CreateAes(ProgramOptions options)
        {
            if (options.EncryptionPassword.IsNullOrEmpty())
                return null;

            var salt = Convert.FromBase64String("38IxHAPayj+fEPTV6ON0AQ==");
            using (var kdf = new Rfc2898DeriveBytes(options.EncryptionPassword, salt))
            {
                var aes = new AesCryptoServiceProvider();
                var keyLength = aes.Key.Length;
                var ivLength = aes.IV.Length;
                var bytes = kdf.GetBytes(keyLength + ivLength);
                aes.Key = bytes.SubArray(0, keyLength);
                aes.IV = bytes.SubArray(keyLength, ivLength);
                return aes;
            }
        }

        private static void RunClient(ProgramOptions options)
        {
            using (var client = new TcpClient(new IPEndPoint(options.Host,options.Port)))
            using (var netStream = client.GetStream())
            using (var compStream = options.Compression.HasValue ? new DeflateStream(netStream, CompressionMode.Decompress) : null)
            using (var aes = CreateAes(options))
            using (var dec = aes?.CreateDecryptor())
            using (var cryptoStream = aes != null ? new CryptoStream((Stream)compStream ?? netStream, dec, CryptoStreamMode.Read) : null)
            using (var reader = new BinaryReader(cryptoStream ?? (Stream)compStream ?? netStream))
            {
                const long bufferSize = 10485760;
                var buffer = new byte[bufferSize];

                while (true)
                {
                    var path = reader.ReadString();
                    Console.WriteLine(path);

                    var length = reader.ReadInt64();
                    var file = options.Directory.GetFile(path);

                    if (!file.Directory.Exists)
                        file.Directory.Create();

                    using (var fileStream = file.OpenWrite())
                    {
                        for (long i = 0; i < length;)
                        {
                            var remaining = length - i;
                            var toRead = (int)Math.Min(remaining, bufferSize);
                            var read = reader.BaseStream.Read(buffer, 0, Math.Min(buffer.Length, toRead));
                            fileStream.Write(buffer, 0, read);
                            i += read;
                        }
                    }

                }
            }
        }
    }
}
