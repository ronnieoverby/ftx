using SecurityDriven.Inferno;
using SecurityDriven.Inferno.Kdf;
using System;
using System.IO;
using System.Net.Sockets;

namespace ftx
{
    public delegate void UpdateFileProgress( long totalBytes,  long deltaBytes);

    public static class Extensions
    {
        public const int DefaultStreamCopyBufferSize = 81920;

        public static long CopyTo(this Stream source, Stream destination, long count, byte[] buffer, UpdateFileProgress updateProgress)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (destination == null) throw new ArgumentNullException(nameof(destination));

            long i = 0;
            while (i < count)
            {
                var n = (int)Math.Min(count - i, buffer.Length);
                var read = source.Read(buffer, 0, Math.Min(buffer.Length, n));
                destination.Write(buffer, 0, read);
                i += read;
                updateProgress(i, read);
            }

            return i;
        }

        public static int GetPort(this TcpListener listener) =>
            ((dynamic)listener.LocalEndpoint).Port;

        public static FileInfo GetFile(this DirectoryInfo directoryInfo, string relativeFilePath) =>
            new FileInfo(Path.Combine(directoryInfo.FullName, relativeFilePath));

        public static byte[] DeriveKey(this string pw, int length = 32)
        {
            var salt = Guid.Parse("74df2c3d-038a-48cd-99b9-223f97dce792").ToByteArray();
            using var kdf = new PBKDF2(SuiteB.HmacFactory, pw, salt);
            return kdf.GetBytes(length);
        }
    }
}