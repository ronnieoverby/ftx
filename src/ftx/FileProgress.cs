using System.Diagnostics;

namespace ftx
{
    public class FileProgress
    {
        public string File { get;  }
        public long BytesTransferred { get; set; }
        public Stopwatch Stopwatch { get; } = new Stopwatch();
        public long Length { get;  }


        public FileProgress(string file, long length)
        {
            File = file;
            Length = length;
        }

        public double PercentComplete =>(double) BytesTransferred/Length;
    }
}