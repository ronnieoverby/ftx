using Humanizer;
using Humanizer.Localisation;
using System;
using System.Collections.Generic;
using static System.Console;
using ByteSize = Humanizer.Bytes.ByteSize;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace ftx
{
    internal class Display
    {
        private readonly int _port;
        private const int _maxByteStamps = 1000;
        private DateTimeOffset _lastRefresh;
        private readonly LinkedList<(long ByteCount, DateTimeOffset Time)> _byteStamps = 
            new LinkedList<(long, DateTimeOffset)>();

        public Display(ProgramOptions options, int port)
        {
            _port = port;
            Options = options;
        }

        public TimeSpan? Delay { get; set; }
        public FileProgress CurrentFileProgress { get; set; }
        public int FileCount { get; set; }

        public long ByteCount
        {
            get => _byteStamps.Last?.Value.ByteCount ?? 0;
            set
            {
                _byteStamps.AddLast((value, DateTimeOffset.Now));
                if (_byteStamps.Count > _maxByteStamps)
                    _byteStamps.RemoveFirst();
            }
        }

        public Stopwatch Stopwatch { get; } = new Stopwatch();
        public ProgramOptions Options { get; }

        public ByteSize BytesPerSecond
        {
            get
            {
                var last = _byteStamps.Last;

                if (last == null)
                    return 0.Bytes();

                var first = _byteStamps.First;
                var span = last.Value.Time - first.Value.Time;
                var count = last.Value.ByteCount - first.Value.ByteCount;
                return (count/span.TotalSeconds).Bytes();
            }
        }

        public void AttemptRefresh()
        {
            if (DateTimeOffset.Now - _lastRefresh < Delay)
                return;

            Clear();

            WriteLine($"Mode:        {Options.ProgramMode}");
            WriteLine($"Host/Port:   {Options.Host}:{_port}");
            WriteLine($"Path:        {Options.Directory.FullName}");

            string compression;

            if (Options.Compression == null)
                compression = "Disabled";
            else if (Options.ProgramMode == ProgramMode.Client)
                compression = "Enabled";
            else
                compression = Options.Compression.ToString();

            WriteLine($"Compression: {compression}");
            WriteLine($"Encryption:  {(Options.Encrypt ? "Enabled" : "Disabled")}");

            if (Options.ProgramMode == ProgramMode.Client)
                WriteLine($"Overwrite:   {(Options.Overwrite ? "Enabled" : "Disabled")}");

            WriteLine();
            WriteLine($"Files:       {FileCount:N0}");
            WriteLine($"Transferred: {ByteCount.Bytes().Humanize("#.###")}");
            WriteLine($"Time:        {Stopwatch.Elapsed.Humanize(100, minUnit: TimeUnit.Second)}");
            WriteLine($"Speed:       {BytesPerSecond.Humanize("#.##")}/s");

            if (CurrentFileProgress != null)
            {
                WriteLine();
                WriteLine("Current File:");
                WriteLine(CurrentFileProgress.File);

                WriteLine(
                    $"{CurrentFileProgress.BytesTransferred.Bytes().Humanize("#.###")} / {CurrentFileProgress.Length.Bytes().Humanize("#.###")} ({CurrentFileProgress.PercentComplete:P})");
            }

            _lastRefresh = DateTimeOffset.Now;
        }

        public void UpdateProgress((long total, long sinceLastUpdate) progressUpdate)
        {
            ByteCount += progressUpdate.sinceLastUpdate;
            CurrentFileProgress.BytesTransferred = progressUpdate.total;
            AttemptRefresh();
        }
    }
}