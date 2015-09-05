using System.Diagnostics;
using System.IO;

namespace ftx
{
    public class FileProgress
    {
        public FileInfo File { get;  }
        public long BytesSent { get; set; }
        public Stopwatch Stopwatch { get; } = new Stopwatch();

        public FileProgress(FileInfo file)
        {
            File = file;
        }

        public double PercentComplete =>
            (double) BytesSent/File.Length;
    }
}