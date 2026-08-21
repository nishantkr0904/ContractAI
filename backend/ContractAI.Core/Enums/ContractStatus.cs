namespace ContractAI.Core.Enums;

// Maps to the PostgreSQL `contract_status` enum. Database labels are
// UPPER_SNAKE_CASE; ContractAI.Data supplies the name translator that bridges
// them, so member names stay idiomatic C# here.
public enum ContractStatus
{
    Uploaded,
    Parsing,
    ParsedSuccess,
    ParsedError,
    Archived,
}
