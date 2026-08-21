using ContractAI.Core.Enums;

namespace ContractAI.API.Contracts;

// PATCH /clauses/{id}/risk body. Severity arrives as an UPPER_SNAKE_CASE string
// ("LOW") and binds through the enum converter configured in Program.cs; an
// unrecognized value fails deserialization and surfaces as a 400. Validation lives
// in the controller rather than in data annotations: annotations on a positional
// record's parameters are read inconsistently by MVC's validator, so the checks are
// done explicitly against ModelState instead.
public sealed record RiskOverrideRequest(RiskLevel Severity, string Explanation)
{
    // The column is unbounded text, but an override is a human justification, so a
    // ceiling keeps a malformed client from writing an unbounded blob.
    public const int MaxExplanationLength = 4000;
}

// The newly effective risk assessment. updated_at is the new row's creation time:
// overrides are recorded as new rows rather than mutations, so the effective
// score's creation is the moment the clause's risk last changed.
public sealed record RiskOverrideResponse(
    Guid Id,
    Guid ContractClauseId,
    RiskLevel Severity,
    string RuleViolated,
    string Explanation,
    DateTimeOffset UpdatedAt);
