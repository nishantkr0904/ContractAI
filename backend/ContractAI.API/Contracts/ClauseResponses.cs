using ContractAI.Core.Enums;

namespace ContractAI.API.Contracts;

// GET /contracts/{id}/clauses is not paginated; it returns the whole clause set of
// one document wrapped in a data envelope.
public sealed record ClauseListResponse(IReadOnlyList<ClauseResponse> Data);

public sealed record ClauseResponse(
    Guid Id,
    Guid ContractId,
    ClauseTypeResponse? ClauseType,
    string RawText,
    int? PageNumber,
    int? ByteOffset,
    double? ConfidenceScore,
    ClauseRiskScoreResponse? RiskScore,
    DateTimeOffset CreatedAt);

public sealed record ClauseTypeResponse(Guid Id, string Name, string? Description);

// The effective (newest) risk assessment nested under a clause. Null until a
// clause has been scored, whether by the AI pipeline or a human override.
public sealed record ClauseRiskScoreResponse(
    Guid Id,
    RiskLevel Severity,
    string RuleViolated,
    string Explanation);
