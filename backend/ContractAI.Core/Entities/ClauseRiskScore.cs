using ContractAI.Core.Enums;

namespace ContractAI.Core.Entities;

// clause_risk_scores: one risk assessment for a clause. The schema allows many
// per clause, so a human override is recorded as a new row rather than a mutation,
// and the newest row is the effective score.
public class ClauseRiskScore
{
    public Guid Id { get; set; }
    public Guid ContractClauseId { get; set; }
    public RiskLevel Severity { get; set; }
    public string RuleViolated { get; set; } = null!;
    public string Explanation { get; set; } = null!;
    public DateTimeOffset CreatedAt { get; set; }

    public ContractClause Clause { get; set; } = null!;
}
