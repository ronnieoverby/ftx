using System;
using System.IO;
using System.IO.Compression;
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
            var listener = new TcpListener(options.EndPoint.ToIpEndPoint());

            listener.Start();

            Console.WriteLine("Waiting for connection.");

            try
            {
                using (var client = listener.AcceptTcpClient())
                using (var netStream = client.GetStream())
                using (var compStream = options.Compress ? new DeflateStream(netStream, CompressionLevel.Fastest) : null)
                using (var aes = CreateAes(options))
                using (var encryptor = aes != null ? aes.CreateEncryptor() : null)
                using (var cryptoStream = aes != null ? new CryptoStream((Stream)compStream ?? netStream, encryptor, CryptoStreamMode.Write) : null)
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
                return new AesCryptoServiceProvider
                {
                    Key = kdf.GetBytes(32),
                    IV = Convert.FromBase64String("65BU4DLIBR2bGbhv8e16YQ==")
                };
        }

        private static void RunClient(ProgramOptions options)
        {
            using (var client = new TcpClient(options.EndPoint.Host, options.EndPoint.Port))
            using (var netStream = client.GetStream())
            using (var compStream = options.Compress ? new DeflateStream(netStream, CompressionMode.Decompress) : null)
            using (var aes = CreateAes(options))
            using (var decryptor = aes != null ? aes.CreateDecryptor() : null)
            using (var cryptoStream = aes != null ? new CryptoStream((Stream)compStream ?? netStream, decryptor, CryptoStreamMode.Read) : null)
            using (var reader = new BinaryReader(cryptoStream ?? (Stream)compStream ?? netStream))
            {
                const long bufferSize = 1024 * 1024 * 10;
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
                        for (long i = 0; i < length; )
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
