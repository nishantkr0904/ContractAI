namespace ContractAI.Core.Enums;

// The ordinals are ABI, not an implementation detail: ClauseOutput.category
// crosses the C-ABI as this ordinal, so the members must stay in the same order
// as contractai::ClauseCategory in parser/include/KeywordTrie.h. Append only.
public enum ClauseCategory : byte
{
    Indemnification = 0,
    PaymentTerms = 1,
    LimitationOfLiability = 2,
    Termination = 3,
    Confidentiality = 4,
    GoverningLaw = 5,
    IntellectualProperty = 6,
    Warranty = 7,
    ForceMajeure = 8,
    DisputeResolution = 9,
    Assignment = 10,
    DataProtection = 11,
    AutoRenewal = 12,
}
