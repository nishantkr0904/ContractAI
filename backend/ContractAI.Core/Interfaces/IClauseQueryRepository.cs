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

    // Clauses across the tenant's contracts ranked by cosine similarity to the query
    // vector. queryEmbedding must be unit-normalized and match the stored embedding
    // dimension; hits below similarityThreshold are dropped and at most `limit` rows
    // return, nearest first. Tenant scope is enforced exactly as in GetByContractAsync
    // (join through contracts), so no foreign clause can surface.
    Task<IReadOnlyList<ClauseSearchResult>> SearchClausesAsync(
        float[] queryEmbedding,
        Guid tenantId,
        double similarityThreshold,
        int limit,
        CancellationToken cancellationToken = default);
}

// One semantic-search hit. SimilarityScore is 1 - cosine_distance, in [0, 1] for
// unit vectors (higher is closer). ClauseType and PageNumber are nullable for the
// same reasons they are on ContractClause.
public sealed record ClauseSearchResult(
    Guid ClauseId,
    Guid ContractId,
    string ContractFileName,
    string? ClauseType,
    string RawText,
    double SimilarityScore,
    int? PageNumber);
