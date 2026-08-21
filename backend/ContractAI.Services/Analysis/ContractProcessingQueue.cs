using System.Threading.Channels;
using ContractAI.Core.Interfaces;

namespace ContractAI.Services.Analysis;

// In-process work queue backing the upload endpoint's 202 response. Bounded so a
// burst of uploads exerts backpressure on producers instead of growing unbounded;
// the capacity is generous because each item is just a Guid.
//
// A durable queue (a table, or a broker) would survive a process restart; this
// deliberately does not, which is the known limitation of in-process background
// work and is acceptable for the single-node target.
public sealed class ContractProcessingQueue : IContractProcessingQueue
{
    private readonly Channel<Guid> _channel = Channel.CreateBounded<Guid>(
        new BoundedChannelOptions(capacity: 1024)
        {
            SingleReader = true,
            SingleWriter = false,
        });

    public ValueTask EnqueueAsync(Guid contractId, CancellationToken cancellationToken = default) =>
        _channel.Writer.WriteAsync(contractId, cancellationToken);

    public ValueTask<Guid> DequeueAsync(CancellationToken cancellationToken) =>
        _channel.Reader.ReadAsync(cancellationToken);
}
