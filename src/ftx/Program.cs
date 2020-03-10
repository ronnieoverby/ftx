using SecurityDriven.Inferno;
using System;
using System.IO;
using System.IO.Compression;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using static ftx.Extensions;
using DisplayQueue = ftx.ActionQueue<ftx.Display>;

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
            var display = new DisplayQueue(new Display(options, listener.GetPort()));
            using var displayTask = display.ProcessQueue();
            using var cts = new CancellationTokenSource();
            var refreshTask = RefreshDisplay(display, cts.Token);

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

                display.Queue(d => d.Stopwatch.Start());

                foreach (var file in options.Directory.EnumerateFiles("*", new EnumerationOptions
                {
                    IgnoreInaccessible = true,
                    RecurseSubdirectories = true
                }))
                {
                    var fileRelPath = file.FullName.Substring(directoryPath.Length);
                    var fileProgress = new FileProgress(fileRelPath, file.Length);
                    display.Queue(d => d.CurrentFileProgress = fileProgress);

                    writer.Write(fileRelPath);
                    writer.Write(file.Length);

                    void UpdateProgress(long total, long delta) =>
                        display.Queue(d => d.UpdateFileProgress(total, delta));

                    using (var fileStream = file.OpenRead())
                        fileStream.CopyTo(writer.BaseStream, file.Length, buffer, UpdateProgress);

                    display.Queue(d => d.FileCount++);
                    //display.Queue(d => d.Refresh());
                }
                Console.Beep();

                display.Queue(d => d.Refresh(/*false*/));
                cts.Cancel();
                display.Complete();
                displayTask.Wait();
            }
            finally
            {
                listener.Stop();
            }
        }

        private static void RunClient(ProgramOptions options)
        {
            var display = new DisplayQueue(new Display(options, options.Port));
            using var displayTask = display.ProcessQueue();
            display.Queue(d => d.Refresh(/*false*/));
            using var cts = new CancellationTokenSource();
            var refreshTask = RefreshDisplay(display, cts.Token);

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
            display.Queue(d => d.Stopwatch.Start());

            var buffer = new byte[DefaultStreamCopyBufferSize];

            while (decryptor?.IsComplete != true)
            {
                //display.Queue(d => d.Refresh());

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
                var fileProgress = new FileProgress(path, length);
                display.Queue(d => d.CurrentFileProgress = fileProgress);


                void UpdateProgress(long total, long delta) =>
                    display.Queue(d => d.UpdateFileProgress(total, delta));

                var skipFile = file.Exists && !options.Overwrite;
                if (skipFile)
                {
                    reader.BaseStream.CopyTo(Stream.Null, length, buffer, UpdateProgress);
                }
                else
                {
                    if (!file.Directory.Exists)
                        file.Directory.Create();

                    using var fileStream = file.Open(FileMode.Create, FileAccess.Write, FileShare.None);
                    fileStream.SetLength(length);
                    reader.BaseStream.CopyTo(fileStream, length, buffer, UpdateProgress);
                }

                display.Queue(d => d.FileCount++);
            }

            Console.Beep();
            
            display.Queue(d => d.Refresh(/*false*/));
            cts.Cancel();
            display.Complete();
            displayTask.Wait();
        }

        static Task RefreshDisplay(DisplayQueue display, CancellationToken cancellationToken) =>
            Task.Run(async () =>
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    display.Queue(d => d.Refresh(), cancellationToken);
                    await Task.Delay(500, cancellationToken);
                }
            }, cancellationToken);
    }
}
