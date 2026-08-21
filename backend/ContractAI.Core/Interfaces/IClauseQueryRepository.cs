using ContractAI.Core.Entities;

namespace ContractAI.Core.Interfaces;

// Read-only clause queries, served by Dapper in ContractAI.Data. Reads are kept
// off EF Core deliberately: this is the dashboard hot path and needs no change
// tracking (ARCHITECTURE.md 3.3, read/write segregation).
public interface IClauseQueryRepository
{
    // Clauses of one contract, ordered by position in the document.
    //
    // tenantId is required rather than inferred: a contract belonging to another
    // tenant yields an empty list, so the caller cannot enumerate foreign data
    // even if the RLS session variable was never set. A soft-deleted contract
    // yields an empty list for the same reason.
    //
    // Each clause carries its ClauseType and, in RiskScores, at most its newest
    // risk assessment. Embedding is left null; fetching 1536 floats per row would
    // dominate the payload and no read path needs it. Contract is not populated —
    // the caller already knows which contract it asked for.
    Task<IReadOnlyList<ContractClause>> GetByContractAsync(
        Guid contractId,
        Guid tenantId,
        CancellationToken cancellationToken = default);
}
