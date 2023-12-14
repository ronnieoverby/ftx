using SecurityDriven.Inferno;
using System;
using System.IO;
using System.IO.Compression;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Threading.Channels;
using System.Threading.Tasks;
using static ftx.Extensions;

namespace ftx;

public static class Program
{
    public static Task Main(string[] args)
    {
        var options = ProgramOptions.FromArgs(args);
        switch (options.ProgramMode)
        {
            case ProgramMode.Server:
                return RunServer(options);
            case ProgramMode.Client:
                RunClient(options);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        return Task.CompletedTask;
    }

    private static async Task RunServer(ProgramOptions options)
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

            using var client = await listener.AcceptTcpClientAsync();
            await using var netStream = client.GetStream();
            await using var compStream = options.Compression.HasValue
                ? new DeflateStream(netStream, options.Compression.Value)
                : default;
            using var encryptor = options.Encrypt
                ? new EtM_EncryptTransform(options.PSK)
                : default;
            await using var cryptoStream = encryptor != null
                ? new CryptoStream((Stream)compStream ?? netStream, encryptor, CryptoStreamMode.Write)
                : null;
            await using var writer = new BinaryWriter(cryptoStream ?? (Stream)compStream ?? netStream);

            display.Stopwatch.Start();

            var fileChannel = Channel.CreateBounded<(FileInfo File, FileStream Stream)>(new BoundedChannelOptions(33)
                { SingleReader = true, SingleWriter = true });

            var fileProducer = Task.Run(async () =>
            {
                try
                {
                    foreach (var file in options.Directory.EnumerateFiles("*", new EnumerationOptions
                             {
                                 IgnoreInaccessible = true,
                                 RecurseSubdirectories = true
                             }))
                    {
                        var stream = file.OpenRead();
                        await fileChannel.Writer.WriteAsync((file, stream));
                    }
                }
                finally
                {
                    fileChannel.Writer.TryComplete();
                }
            });

            await foreach (var (file, fileStream) in fileChannel.Reader.ReadAllAsync())
            {
                await using var _ = fileStream;
                
                var fileRelPath = file.FullName[directoryPath.Length..];
                display.CurrentFileProgress = new FileProgress(fileRelPath, file.Length);

                writer.Write(fileRelPath);
                writer.Write(file.Length);
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