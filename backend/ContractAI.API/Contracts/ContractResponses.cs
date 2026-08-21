using ContractAI.Core.Enums;

namespace ContractAI.API.Contracts;

// A page of a collection plus the envelope shape API_REFERENCE.md specifies:
// data alongside a meta block carrying the paging counters.
public sealed record PagedResponse<T>(IReadOnlyList<T> Data, PaginationMeta Meta);

public sealed record PaginationMeta(int CurrentPage, int TotalPages, int TotalRecords);

// List item for GET /contracts. Omits uploaded_by; the single-contract resource
// carries that.
public sealed record ContractSummaryResponse(
    Guid Id,
    string FileName,
    string FileUri,
    ContractStatus Status,
    RiskLevel OverallRisk,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

// GET /contracts/{id}: the summary fields plus uploaded_by.
public sealed record ContractDetailResponse(
    Guid Id,
    Guid? UploadedBy,
    string FileName,
    string FileUri,
    ContractStatus Status,
    RiskLevel OverallRisk,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
