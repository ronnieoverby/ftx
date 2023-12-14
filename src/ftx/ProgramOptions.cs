using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;

namespace ftx;

public class ProgramOptions
{
    public ProgramMode ProgramMode { get; private set; }
    public DirectoryInfo Directory { get; private set; }
    public IPAddress Host { get; private set; }
    public int Port { get; private set; }
    public CompressionLevel? Compression { get; private set; }
    public bool Encrypt => PSK?.Length > 0;
    public byte[] PSK { get; private set; }
    public bool Overwrite { get; private set; }

    public static ProgramOptions FromArgs(string[] args)
    {
        var options = new ProgramOptions
        {
            ProgramMode = Parse(args, "mode", x => Enum.Parse<ProgramMode>(x[1], true)),
            Directory = Parse(args, "path", x => new DirectoryInfo(x[1]))
        };
        
        options.Host = Parse(args, "host", x =>
        {
            var value = x.ElementAtOrDefault(1);
            return value == null
                ? options.ProgramMode == ProgramMode.Server ? IPAddress.Any : IPAddress.Loopback
                : Dns.GetHostAddresses(value)[0];
        });
        options.Port = Parse(args, "port", x =>
        {
            var value = x.ElementAtOrDefault(1);
            return value != null ? int.Parse(value) : 0;
        });
        options.Compression = Parse(args, "compression", x =>
        {
            if (x.Length == 0) 
                return default(CompressionLevel?);

            var level = x.ElementAtOrDefault(1);
            return string.IsNullOrWhiteSpace(level)
                ? CompressionLevel.Fastest
                : Enum.Parse<CompressionLevel>(level, true);
        });

        options.PSK = Parse(args, "psk", x => x.Length == 0 ? default : x.ElementAtOrDefault(1).DeriveKey());

        options.Overwrite = Parse(args, "overwrite", ParseBool);

        return options;
    }

    private static bool ParseBool(string[] x) => x.Length switch
    {
        0 => false,
        1 => true,
        _ => bool.Parse(x[1]),
    };

    private static T Parse<T>(string[] args, string token, Func<string[], T> func)
    {
        var pattern = $@"^[/-]{Regex.Escape(token)}";
        var i = Array.FindIndex(args, s => Regex.IsMatch(s, pattern));

        if (i == -1)
            return func(Array.Empty<string>());

        var values = args.Skip(i);

        var z = Array.FindIndex(args, i + 1, s => Regex.IsMatch(s, @"^[/-]", RegexOptions.IgnoreCase));
        if (z != -1)
            values = values.Take(z - i);

        try
        {
            return func(values.ToArray());
        }
        catch (Exception ex)
        {
            Console.WriteLine("Trouble parsing CLI argument: {0}", token);
            Console.WriteLine(ex);
            throw;
        }
    }
}