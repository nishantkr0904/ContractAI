namespace ContractAI.Core.Interfaces;

// Hands a freshly uploaded contract off to background analysis. The upload
// endpoint enqueues and returns 202 immediately; the worker drains the queue so
// parsing never blocks the request thread.
public interface IContractProcessingQueue
{
    ValueTask EnqueueAsync(Guid contractId, CancellationToken cancellationToken = default);

    ValueTask<Guid> DequeueAsync(CancellationToken cancellationToken);
}
