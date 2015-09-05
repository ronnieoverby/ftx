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
            var display = new Display(options) {Delay = 1.Seconds()};
            var listener = new TcpListener(options.Host, options.Port);
            listener.Start();
            int port = ((dynamic)listener.LocalEndpoint).Port;

            try
            {
                Console.WriteLine($"Listening on port {port}.");

                var files = options.Directory.EnumerateFiles("*", SearchOption.AllDirectories);

                var buffer = new byte[Extensions.DefaultStreamCopyBufferSize];

                using (var it = new BufferedEnumerator<FileInfo>(files.GetEnumerator(), 1))
                {
                    using (var client = listener.AcceptTcpClient())
                    using (var netStream = client.GetStream())
                    using (
                        var compStream = options.Compression.HasValue
                            ? new DeflateStream(netStream, options.Compression.Value)
                            : null)
                    using (var aes = CreateAes(options))
                    using (var enc = aes?.CreateEncryptor())
                    using (
                        var cryptoStream = aes != null
                            ? new CryptoStream((Stream)compStream ?? netStream, enc, CryptoStreamMode.Write)
                            : null)
                    using (var writer = new BinaryWriter(cryptoStream ?? (Stream)compStream ?? netStream))
                    {
                        while (it.MoveNext())
                        {
                            var file = it.Current;
                            display.CurrentFile = new FileProgress(file)
                                .Do(x => x.Stopwatch.Start());

                            var path = file.GetRelativePathFrom(options.Directory);
                            Console.WriteLine(path);

                            try
                            {
                                writer.Write(path);
                                writer.Write(file.Length);

                                using (var fileStream = file.OpenRead())
                                    fileStream.CopyTo(writer.BaseStream, file.Length, buffer, new Progress<long>(
                                        b =>
                                        {
                                            display.ByteCount += (display.CurrentFile.BytesSent = b);
                                            display.Refresh();
                                        }));

                                display.FileCount++;
                                display.Refresh();
                            }
                            catch (Exception ex)
                            {
                                if (!it.MovePrevious())
                                    throw;

                                Console.WriteLine(ex);
                                Console.WriteLine("Press enter to resume.");
                                Console.ReadLine();
                            }
                        }
                    }
                }

            }
            finally
            {
                listener.Stop();
            }
        }

        private static void RunClient(ProgramOptions options)
        {
            using (var client = new TcpClient().Do(c => c.Connect(options.Host, options.Port)))
            using (var netStream = client.GetStream())
            using (var compStream = options.Compression.HasValue ? new DeflateStream(netStream, CompressionMode.Decompress) : null)
            using (var aes = CreateAes(options))
            using (var dec = aes?.CreateDecryptor())
            using (var cryptoStream = aes != null ? new CryptoStream((Stream)compStream ?? netStream, dec, CryptoStreamMode.Read) : null)
            using (var reader = new BinaryReader(cryptoStream ?? (Stream)compStream ?? netStream))
            using (var nullStream = new NullStream())
            {
                var buffer = new byte[Extensions.DefaultStreamCopyBufferSize];

                while (true)
                {
                    string path;
                    try
                    {
                        path = reader.ReadString();
                    }
                    catch (EndOfStreamException)
                    {
                        break;
                    }

                    Console.WriteLine(path);

                    var length = reader.ReadInt64();
                    var file = options.Directory.GetFile(path);

                    if (file.Exists && !options.Overwrite)
                    {
                        reader.BaseStream.CopyTo(nullStream, length, buffer);
                    }
                    else
                    {
                        if (!file.Directory.Exists)
                            file.Directory.Create();

                        using (var fileStream = file.Open(FileMode.Create, FileAccess.Write, FileShare.None))
                            reader.BaseStream.CopyTo(fileStream, length, buffer);
                    }
                }
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
    }
}
