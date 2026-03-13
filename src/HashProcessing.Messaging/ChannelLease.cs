using RabbitMQ.Client;

namespace HashProcessing.Messaging;

public sealed class ChannelLease(IChannel channel, Action<IChannel> returnToPool) : IAsyncDisposable
{
    private Action<IChannel>? _returnToPool = returnToPool ?? throw new ArgumentNullException(nameof(returnToPool));

    public IChannel Channel { get; } = channel ?? throw new ArgumentNullException(nameof(channel));

    public ValueTask DisposeAsync()
    {
        var callback = Interlocked.Exchange(ref _returnToPool, null);
        callback?.Invoke(Channel);
        return ValueTask.CompletedTask;
    }
}
