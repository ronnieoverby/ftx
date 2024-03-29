﻿using Humanizer;
using Humanizer.Bytes;
using Humanizer.Localisation;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using static System.Console;

namespace ftx;

internal class Display(ProgramOptions options, int port)
{
    private const int MaxStamps = 100;
    private readonly TimeSpan _stampDelay = TimeSpan.FromSeconds(.1);
    private readonly TimeSpan _refreshDelay = TimeSpan.FromSeconds(.5);
    private readonly Stopwatch _displayWatch = Stopwatch.StartNew();
    private readonly Stopwatch _stampWatch = Stopwatch.StartNew();
    private readonly LinkedList<(long ByteCount, DateTimeOffset Time)> _stamps = [];

    public FileProgress CurrentFileProgress { get; set; }
    public int FileCount { get; set; }

    private long _byteCount;
    public long ByteCount
    {
        get => _byteCount;
        set
        {
            _byteCount = value;

            if (_stampWatch.Elapsed >= _stampDelay)
            {
                _stamps.AddLast((value, DateTimeOffset.Now));
                if (_stamps.Count > MaxStamps)
                    _stamps.RemoveFirst();

                _stampWatch.Restart();
            }
        }
    }

    public Stopwatch Stopwatch { get; } = new();
    public ProgramOptions Options { get; } = options;

    public ByteSize RecentBytesPerSecond
    {
        get
        {
            var last = _stamps.Last;

            if (last == null)
                return 0.Bytes();

            var first = _stamps.First;
            var span = last.Value.Time - first.Value.Time;
            var count = last.Value.ByteCount - first.Value.ByteCount;
            return (count / span.TotalSeconds).Bytes();
        }
    }

    public ByteSize SustainedBytesPerSecond => ByteCount switch
    {
        0 => 0.Bytes(),
        var byteCount => (byteCount / Stopwatch.Elapsed.TotalSeconds).Bytes()
    };

    public void Refresh(in bool observeDelay = true)
    {
        if (observeDelay && _displayWatch.Elapsed < _refreshDelay)
            return;

        Clear();

        WriteLine($"Mode:        {Options.ProgramMode}");
        WriteLine($"Host/Port:   {Options.Host}:{port}");
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
        WriteLine($"Speed:       {RecentBytesPerSecond.Humanize("#.##")}/s");
        WriteLine($"Avg. Speed:  {SustainedBytesPerSecond.Humanize("#.##")}/s");

        if (CurrentFileProgress != null)
        {
            WriteLine();
            WriteLine("Current File:");
            WriteLine(CurrentFileProgress.File);

            WriteLine(
                $"{CurrentFileProgress.BytesTransferred.Bytes().Humanize("#.###")} / {CurrentFileProgress.Length.Bytes().Humanize("#.###")} ({CurrentFileProgress.PercentComplete:P})");
        }

        _displayWatch.Restart();
    }

    public void UpdateFileProgress(in long totalBytes, in long deltaBytes)
    {
        ByteCount += deltaBytes;
        CurrentFileProgress.BytesTransferred = totalBytes;
        Refresh();
    }
}