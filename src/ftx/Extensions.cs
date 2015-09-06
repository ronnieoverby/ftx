using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;

namespace ftx
{
    public static class Extensions
    {
        public const int DefaultStreamCopyBufferSize = 81920;

        public static long CopyTo(this Stream source, Stream destination, long count, byte[] buffer, Action<long> progress = null)
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
                progress?.Invoke(i);
            }

            return i;

        }
        public static long CopyTo(this Stream source, Stream destination, long count, int bufferSize = DefaultStreamCopyBufferSize, Action<long> progress = null)
        {
            return CopyTo(source, destination, count, new byte[bufferSize], progress);
        }

        public static byte[] SubArray(this byte[] source, int index, int length)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            var sub = new byte[length];
            Array.Copy(source, index, sub, 0, length);
            return sub;

        }

        public static T Do<T>(this T obj, Action<T> action)
        {
            action(obj);
            return obj;
        }

        public static TOut Get<TIn, TOut>(this TIn obj, Func<TIn, TOut> func) => func(obj);


        public static string TrimDirSeps(this string path)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));

            return path.TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        public static IEnumerable<DirectoryInfo> SelfAndAllSubDirectories(this DirectoryInfo di)
        {
            yield return di;
            foreach (var sub in di.EnumerateDirectories("*", SearchOption.AllDirectories))
                yield return sub;
        }

        public static int GetPort(this TcpListener listener)
        {
            return ((dynamic) listener.LocalEndpoint).Port;
        }
    }
}