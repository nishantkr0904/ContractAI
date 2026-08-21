namespace ContractAI.Core.Interfaces;

// Orchestrates the analysis of one already-uploaded contract: read the stored PDF,
// extract clauses through the native engine, and persist them. Invoked off the
// request thread by the processing worker, so it takes only the contract id and
// resolves everything else from storage.
public interface IContractAnalysisService
{
    Task ProcessContractAsync(Guid contractId, CancellationToken cancellationToken = default);
}
