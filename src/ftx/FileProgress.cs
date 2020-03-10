using System.Diagnostics;

namespace ftx
{
    public class FileProgress
    {
        public Stopwatch Stopwatch { get; } = Stopwatch.StartNew();
        public string File { get;  }
        public long BytesTransferred { get; set; }
        public long Length { get;  }

        public FileProgress(string file, long length)
        {
            File = file;
            Length = length;
        }

        public double PercentComplete =>(double) BytesTransferred/Length;
    }
}