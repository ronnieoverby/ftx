namespace ftx;

public class FileProgress(string file, long length)
{
    public string File { get;  } = file;
    public long BytesTransferred { get; set; }
    public long Length { get;  } = length;

    public double PercentComplete =>(double) BytesTransferred/Length;
}