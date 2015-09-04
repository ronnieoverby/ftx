using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using CoreTechs.Common;

namespace ftx
{
    public class ProgramOptions
    {
        public ProgramMode ProgramMode { get; set; }
        public DirectoryInfo Directory { get; set; }
        public DnsEndPoint EndPoint { get; set; }
        public CompressionLevel? Compression { get; set; }
        public string EncryptionPassword { get; set; }

        public static ProgramOptions FromArgs(string[] args)
        {
            return new ProgramOptions
            {
                ProgramMode = Parse(args, "mode", x => ParseEnum<ProgramMode>(x[1])),
                Directory = Parse(args, "path", x => new DirectoryInfo(x[1])),
                EndPoint = new DnsEndPoint(Parse(args, "host", x => x[1]), Parse(args, "port", x => int.Parse(x[1]))),
                Compression = Parse(args, "compression", x =>
                {
                    if (!x.Any()) return (CompressionLevel?) null;

                    var level = x.ElementAtOrDefault(1);
                    return level.IsNullOrEmpty()
                        ? CompressionLevel.Fastest
                        : ParseEnum<CompressionLevel>(level);
                }),
                EncryptionPassword = Parse(args, "password", x => x.Any() ? x[1] : null),
            };
        }


        private static T ParseEnum<T>(string s)
        {
            return (T)Enum.Parse(typeof(T), s);
        }

        private static T Parse<T>(string[] args, string token, Func<string[], T> func)
        {
            var pattern = string.Format(@"^[/-]{0}", Regex.Escape(token));
            var i = Array.FindIndex(args, s => Regex.IsMatch(s, pattern));

            if (i == -1)
                return func(new string[0]);

            var values = args.Skip(i);

            var z = Array.FindIndex(args, i + 1, s => Regex.IsMatch(s, @"^[/-]", RegexOptions.IgnoreCase));

            if (z != -1)
            {
                values = values.Take(z-i);
            }

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
}