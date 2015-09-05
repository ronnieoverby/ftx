using System;
using System.Collections.Generic;
using CoreTechs.Common;
using Humanizer;
using Humanizer.Localisation;
using static System.Console;
using ByteSize = Humanizer.Bytes.ByteSize;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace ftx
{
    internal class Display
    {
        private const int MaxByteStamps = 1000;
        private DateTimeOffset _lastRefresh;
        private readonly LinkedList<ByteStamp> _byteStamps = new LinkedList<ByteStamp>();

        public Display(ProgramOptions options)
        {
            Options = options;
        }

        public TimeSpan? Delay { get; set; }
        public FileProgress CurrentFile { get; set; }
        public int FileCount { get; set; }

        public long ByteCount
        {
            get { return _byteStamps.Last?.Value.ByteCount ?? 0; }
            set
            {
                _byteStamps.AddLast(new ByteStamp(value));
                if (_byteStamps.Count > MaxByteStamps)
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
                var first = _byteStamps.First;
                var span = last.Value.DateTime - first.Value.DateTime;
                var count = last.Value.ByteCount - first.Value.ByteCount;
                return (count/span.TotalSeconds).Bytes();
            }
        }

        public void Refresh()
        {
            if (DateTimeOffset.Now - _lastRefresh < Delay)
                return;

            Clear();

            WriteLine($"Mode:        {Options.ProgramMode}");
            WriteLine($"Host/Port:   {Options.Host}:{Options.Port}");
            WriteLine($"Path:        {Options.Directory.FullName}");
            WriteLine($"Compression: {Options.Compression?.ToString() ?? "Off"}");
            WriteLine($"Encryption:  {(Options.EncryptionPassword.IsNullOrEmpty() ? "Off" : "On")}");

            if (Options.ProgramMode == ProgramMode.Client)
                WriteLine($"Overwrite:   {(Options.Overwrite ? "On" : "Off")}");

            WriteLine($"Files:       {FileCount:N0}");
            WriteLine($"Transferred: {ByteCount.Bytes().Humanize("#.###")}");
            WriteLine($"Time:        {Stopwatch.Elapsed.Humanize(minUnit: TimeUnit.Second)}");
            WriteLine($"Speed:       {BytesPerSecond.Humanize("#.##")}/s");

            if (CurrentFile != null)
            {
                WriteLine();
                WriteLine("Current File:");
                WriteLine(CurrentFile.File);

                WriteLine(
                    $"{CurrentFile.BytesTransferred.Bytes().Humanize("#.###")} / {CurrentFile.Length.Bytes().Humanize("#.###")} ({CurrentFile.PercentComplete:P})");
            }

            _lastRefresh = DateTimeOffset.Now;
        }
    }
}