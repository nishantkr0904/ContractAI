using ContractAI.Core.Enums;

namespace ContractAI.API.Contracts;

// Response bodies mirror API_REFERENCE.md. Property names are serialized
// snake_case and enum values UPPER_SNAKE_CASE by the JSON options configured in
// Program.cs, so the C# members stay idiomatic here.

// 202 Accepted body for an upload. Links points the client at the status resource
// it should poll while background analysis runs.
public sealed record ContractUploadResponse(
    Guid Id,
    string FileName,
    ContractStatus Status,
    DateTimeOffset CreatedAt,
    ContractUploadLinks Links);

public sealed record ContractUploadLinks(string Status);
