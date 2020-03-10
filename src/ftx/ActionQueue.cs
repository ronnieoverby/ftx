using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using static System.Threading.Channels.Channel;

namespace ftx
{
    internal class ActionQueue<T>
    {
        private readonly T _state;

        private readonly Channel<Action<T>> _channel = CreateUnbounded<Action<T>>(
            new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });

        public ActionQueue(T state) => _state = state;

        public void Queue(Action<T> action, CancellationToken cancellationToken = default) =>
            _channel.Writer.WriteAsync(action, cancellationToken);

        public async Task ProcessQueue()
        {
            await foreach (var action in _channel.Reader.ReadAllAsync())
                action(_state);
        }

        public void Complete() => _channel.Writer.Complete();
    }
}
