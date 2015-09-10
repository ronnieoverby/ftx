using System;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
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
            var display = new Display(options, listener.GetPort()) { Delay = .5.Seconds() };
            display.Refresh();

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
                    using (var clearReader = new BinaryReader( netStream))
                    using (var clearWriter = new BinaryWriter(clearReader.BaseStream))
                    using (var aes = CreateAesForServer(options, clearReader, clearWriter))
                    using (var enc = aes?.CreateEncryptor())
                    using (var cryptoStream = aes != null
                            ? new CryptoStream((Stream) compStream ?? netStream, enc, CryptoStreamMode.Write)
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
            var display = new Display(options, options.Port) { Delay = .5.Seconds() };


            using (var client = new TcpClient().Do(c => c.Connect(options.Host, options.Port)))
            using (var netStream = client.GetStream())
            using (var compStream = options.Compression.HasValue ? new DeflateStream(netStream, CompressionMode.Decompress) : null)
            using (var clearReader = new BinaryReader(netStream))
            using (var clearWriter = new BinaryWriter(clearReader.BaseStream))
            using (var aes = CreateAesForClient(options, clearReader, clearWriter))
            using (var dec = aes?.CreateDecryptor())
            using (var cryptoStream = aes != null ? new CryptoStream((Stream) compStream ?? netStream, dec, CryptoStreamMode.Read) : null)
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

        private static Aes CreateAesForServer(ProgramOptions options, BinaryReader reader, BinaryWriter writer)
        {
            if (!options.Encrypt) return null;


            using (var ke = new ECDiffieHellmanCng())
            {
                // expecting client's public key

                var len = reader.ReadInt32();
                byte[] key;

                using (var remotePubKey = CngKey.Import(reader.ReadBytes(len), CngKeyBlobFormat.EccPublicBlob))
                    key = ke.DeriveKeyMaterial(remotePubKey);

                var aes = new AesCryptoServiceProvider();
                using (var kdf = new Rfc2898DeriveBytes(key, Salt.ToArray(), KdfIterations))
                    aes.Key = kdf.GetBytes(aes.KeySize / 8);

                // send pub key and IV to client
                var localPubKey = ke.PublicKey.ToByteArray();
                writer.Write(localPubKey.Length);
                writer.Write(localPubKey);
                writer.Write(aes.IV.Length);
                writer.Write(aes.IV);

                return aes;
            }
        }

        private const int KdfIterations = 1000;

        private static readonly ReadOnlyCollection<byte> Salt =
            Array.AsReadOnly(Convert.FromBase64String("hkuDTnecxj+oDytliJ69BQ=="));

        private static Aes CreateAesForClient(ProgramOptions options, BinaryReader reader, BinaryWriter writer)
        {
            if (!options.Encrypt)
                return null;


            using (var ke = new ECDiffieHellmanCng())
            {
               // send our public key

                var localPubKey = ke.PublicKey.ToByteArray();
                writer.Write(localPubKey.Length);
                writer.Write(localPubKey);

                var aes = new AesCryptoServiceProvider();

                // expecting server's public key

                var len = reader.ReadInt32();
                byte[] key;

                using (var remotePublicKey = CngKey.Import(reader.ReadBytes(len), CngKeyBlobFormat.EccPublicBlob))
                    key = ke.DeriveKeyMaterial(remotePublicKey);

                using (var kdf = new Rfc2898DeriveBytes(key, Salt.ToArray(), KdfIterations))
                    aes.Key = kdf.GetBytes(aes.KeySize/8);

                // expecting IV

                len = reader.ReadInt32();
                aes.IV = reader.ReadBytes(len);

                return aes;
            }
        }
    }
}
