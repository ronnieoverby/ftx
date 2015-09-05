using System;
using CoreTechs.Common;
using static System.Console;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace ftx
{
    internal class Display
    {
        private DateTimeOffset _lastRefresh;
        private readonly string _staticText;
        private static readonly string NL = Environment.NewLine;

        public Display(ProgramOptions options)
        {
            Options = options;

            /*  var mode = options.ProgramMode == ProgramMode.Server ? "Source" : "Destination";
              string compression = $"Compression: {options.Compression?.ToString() ?? "None"}{NL}";
              string encryption = $"Encryption: {(options.EncryptionPassword.IsNullOrEmpty() ? "Off" : "On")}{NL}";
              _staticText =     + compression +
                            encryption;*/

            _staticText = "TODO";
        }

        public TimeSpan? Delay { get; set; }
        public FileProgress CurrentFile { get; set; }
        public int FileCount { get; set; }
        public long ByteCount { get; set; }
        public Stopwatch Stopwatch { get; } = new Stopwatch();
        public ProgramOptions Options { get; }



        public ByteSize BytesPerSecond => ByteSize.FromBytes((long)(ByteCount / Stopwatch.Elapsed.TotalSeconds));

        public void Refresh()
        {
            if (DateTimeOffset.Now - _lastRefresh < Delay)
                return;

            Clear();

            WriteLine($"Time Elapsed: {Stopwatch.Elapsed:g}");
            WriteLine($"Bytes: {ByteCount:N}");
            WriteLine($"Files: {FileCount:N}");
            WriteLine($"Speed: {BytesPerSecond.Kilobytes:N} KB/Sec");

            if (CurrentFile != null)
            {
                WriteLine(CurrentFile.File.FullName);
                WriteLine($"{CurrentFile.BytesSent:N} / {CurrentFile.File.Length:N} ({CurrentFile.PercentComplete:P})");
            }

            _lastRefresh = DateTimeOffset.Now;
        }
    }
}