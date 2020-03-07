using System;
using System.IO;
using System.Net.Sockets;

namespace ftx
{
    public static class Extensions
    {
        public const int DefaultStreamCopyBufferSize = 81920;

        public static long CopyTo(this Stream source, Stream destination, long count, byte[] buffer, Action<(long total, long sinceLastUpdate)> progress = null)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (destination == null) throw new ArgumentNullException(nameof(destination));

            long i = 0;
            while (i < count)
            {
                var n = (int) Math.Min(count - i, buffer.Length);
                var read = source.Read(buffer, 0, Math.Min(buffer.Length, n));
                destination.Write(buffer, 0, read);
                i += read;
                progress?.Invoke((i, read));
            }

            return i;
        }

        public static int GetPort(this TcpListener listener) =>
            ((dynamic)listener.LocalEndpoint).Port;
    }
}