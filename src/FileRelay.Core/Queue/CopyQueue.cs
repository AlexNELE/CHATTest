using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace FileRelay.Core.Queue;

/// <summary>
/// Thread-safe asynchronous queue for copy requests.
/// </summary>
public sealed class CopyQueue : IDisposable
{
    private readonly Channel<CopyRequest> _channel;
    private long _count;

    public CopyQueue()
    {
        _channel = Channel.CreateUnbounded<CopyRequest>(new UnboundedChannelOptions
        {
            SingleReader = false,
            SingleWriter = false,
            AllowSynchronousContinuations = false
        });
    }

    public long Count => Interlocked.Read(ref _count);

    public ValueTask EnqueueAsync(CopyRequest request, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _count);
        return _channel.Writer.WriteAsync(request, cancellationToken);
    }

    public async IAsyncEnumerable<CopyRequest> DequeueAllAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        while (await _channel.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
        {
            while (_channel.Reader.TryRead(out var request))
            {
                Interlocked.Decrement(ref _count);
                yield return request;
            }
        }
    }

    public void Complete() => _channel.Writer.TryComplete();

    public void Dispose()
    {
        Complete();
    }
}
