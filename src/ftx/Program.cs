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
            var display = new Display(options) {Delay = .5.Seconds()}.Do(x => x.Refresh());
            var listener = new TcpListener(options.Host, options.Port);
            listener.Start();

            try
            {

                var files = options.Directory.EnumerateFiles("*", SearchOption.AllDirectories);
                var buffer = new byte[Extensions.DefaultStreamCopyBufferSize];

                using (var it = files.GetEnumerator())
                {
                    using (var client = listener.AcceptTcpClient())
                    using (var netStream = client.GetStream())
                    using (var compStream = options.Compression.HasValue
                            ? new DeflateStream(netStream, options.Compression.Value)
                            : null)
                    using (var aes = CreateAes(options))
                    using (var enc = aes?.CreateEncryptor())
                    using (var cryptoStream = aes != null
                            ? new CryptoStream((Stream)compStream ?? netStream, enc, CryptoStreamMode.Write)
                            : null)
                    using (var writer = new BinaryWriter(cryptoStream ?? (Stream)compStream ?? netStream))
                    {
                        display.Stopwatch.Start();
                        while (it.MoveNext())
                        {
                            var file = it.Current;
                            display.CurrentFile = new FileProgress(
                                file.GetRelativePathFrom(options.Directory),
                                file.Length).Do(x => x.Stopwatch.Start());
                            var path = file.GetRelativePathFrom(options.Directory);

                            writer.Write(path);
                            writer.Write(file.Length);

                            using (var fileStream = file.OpenRead())
                                fileStream.CopyTo(writer.BaseStream, file.Length, buffer,
                                    b =>
                                    {
                                        display.ByteCount += (display.CurrentFile.BytesTransferred = b);
                                        display.Refresh();
                                    });

                            display.FileCount++;
                            display.Refresh();
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
            var display = new Display(options) { Delay = .5.Seconds() };

            using (var client = new TcpClient().Do(c => c.Connect(options.Host, options.Port)))
            using (var netStream = client.GetStream())
            using (var compStream = options.Compression.HasValue ? new DeflateStream(netStream, CompressionMode.Decompress) : null)
            using (var aes = CreateAes(options))
            using (var dec = aes?.CreateDecryptor())
            using (var cryptoStream = aes != null ? new CryptoStream((Stream)compStream ?? netStream, dec, CryptoStreamMode.Read) : null)
            using (var reader = new BinaryReader(cryptoStream ?? (Stream)compStream ?? netStream))
            using (var nullStream = new NullStream())
            {
                display.Stopwatch.Start();

                var buffer = new byte[Extensions.DefaultStreamCopyBufferSize];

                while (true)
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

                    display.CurrentFile = new FileProgress(path, length).Do(x => x.Stopwatch.Start());

                    Action<long> progress = b =>
                    {
                        display.ByteCount += (display.CurrentFile.BytesTransferred = b);
                        display.Refresh();
                    };

                    var skipFile = (file.Exists && !options.Overwrite);
                                   //|| options.ExcludedDirectories.Any(file.IsContainedWithin);

                    if (skipFile)
                    {
                        reader.BaseStream.CopyTo(nullStream, length, buffer, progress);
                    }
                    else
                    {
                        if (!file.Directory.Exists)
                            file.Directory.Create();

                        using (var fileStream = file.Open(FileMode.Create, FileAccess.Write, FileShare.None))
                            reader.BaseStream.CopyTo(fileStream, length, buffer, progress);
                    }

                    display.FileCount++;
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
