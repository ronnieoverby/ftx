using System;

namespace ftx
{
    public static class Extensions
    {


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
    }
}