using System;

namespace ftx
{
    internal class ByteStamp
    {
        public ByteStamp(long byteCount)
        {
            ByteCount = byteCount;
        }

        public long ByteCount { get; }
        public DateTimeOffset DateTime { get; } = DateTimeOffset.Now;
    }
}