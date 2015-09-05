using System.IO;

namespace ftx
{
    internal class NullStream : Stream
    {
        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            using (var ms = new MemoryStream(new byte[0]))
                return ms.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            using (var ms = new MemoryStream(new byte[0]))
                ms.SetLength(value);
        }

        public override int Read(byte[] buffer, int offset, int count) => 0;

        public override void Write(byte[] buffer, int offset, int count)
        {
        }

        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => true;
        public override long Length => 0;

        public override long Position
        {
            get { return 0; }
            set { Seek(value, SeekOrigin.Begin); }
        }
    }
}