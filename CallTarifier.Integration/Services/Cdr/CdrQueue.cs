using System.Threading.Channels;
using CallTarifier.Integration.Models;

namespace CallTarifier.Integration.Services.Cdr;

public interface ICdrQueue
{
    ValueTask WriteAsync(CdrRecord record, CancellationToken cancellationToken = default);

    IAsyncEnumerable<CdrRecord> ReadAllAsync(CancellationToken cancellationToken);
}

public sealed class CdrQueue : ICdrQueue
{
    private readonly Channel<CdrRecord> _channel = Channel.CreateUnbounded<CdrRecord>(
        new UnboundedChannelOptions
        {
            SingleReader = false,
            SingleWriter = false
        });

    public ValueTask WriteAsync(CdrRecord record, CancellationToken cancellationToken = default)
        => _channel.Writer.WriteAsync(record, cancellationToken);

    public IAsyncEnumerable<CdrRecord> ReadAllAsync(CancellationToken cancellationToken)
        => _channel.Reader.ReadAllAsync(cancellationToken);
}
