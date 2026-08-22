namespace ContractAI.API.Contracts;

// POST /search/clauses body. Only query is required; similarity_threshold and limit
// have server defaults and are clamped in the controller. Validation lives in the
// controller (not data annotations) for the same reason as the other request records
// (see RiskOverride.cs): annotations on a positional record's parameters are read
// inconsistently by MVC's validator.
public sealed record SearchClausesRequest(
    string? Query,
    double? SimilarityThreshold,
    int? Limit);

// Mirrors API_REFERENCE.md. similarity_score is 1 - cosine_distance (higher is
// closer); meta reports the wall-clock query time and the distance metric used.
public sealed record SearchClausesResponse(
    IReadOnlyList<SearchClauseResult> Results,
    SearchMeta Meta);

public sealed record SearchClauseResult(
    Guid ClauseId,
    Guid ContractId,
    string ContractFileName,
    string? ClauseType,
    string RawText,
    double SimilarityScore,
    int? PageNumber);

public sealed record SearchMeta(
    long ExecutionTimeMs,
    string VectorDistanceMetric);
