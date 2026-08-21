namespace ContractAI.Core.Enums;

// Maps to the PostgreSQL `risk_level` enum, shared by contracts.overall_risk
// and clause_risk_scores.severity.
public enum RiskLevel
{
    Unknown,
    Low,
    Medium,
    High,
    Critical,
}
