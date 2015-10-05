using System;
using System.Collections.Generic;
using System.Linq;
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
        private readonly int _port;
        private const int MaxByteStamps = 1000;
        private DateTimeOffset _lastRefresh;
        private readonly LinkedList<ByteStamp> _byteStamps = new LinkedList<ByteStamp>();

        public Display(ProgramOptions options, int port)
        {
            _port = port;
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

                if (last == null)
                    return 0.Bytes();

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

      /*      if (Options.ExcludedDirectories.Any())
            {
                WriteLine("Excluded Directories:");
                foreach (var di in Options.ExcludedDirectories)
                    WriteLine($"\t{di.FullName}");
            }*/

            WriteLine();
            WriteLine($"Files:       {FileCount:N0}");
            WriteLine($"Transferred: {ByteCount.Bytes().Humanize("#.###")}");
            WriteLine($"Time:        {Stopwatch.Elapsed.Humanize(100, minUnit: TimeUnit.Second)}");
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