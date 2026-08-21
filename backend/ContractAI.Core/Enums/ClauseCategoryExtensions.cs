namespace ContractAI.Core.Enums;

public static class ClauseCategoryExtensions
{
    // These strings are the canonical taxonomy labels stored in clause_types.name,
    // and they must stay byte-identical to contractai::CategoryName in
    // parser/src/KeywordTrie.cpp. They cannot be derived from the member names
    // because several contain spaces or a hyphen ("Payment Terms", "Auto-Renewal").
    public static string ToClauseTypeName(this ClauseCategory category) => category switch
    {
        ClauseCategory.Indemnification => "Indemnification",
        ClauseCategory.PaymentTerms => "Payment Terms",
        ClauseCategory.LimitationOfLiability => "Limitation of Liability",
        ClauseCategory.Termination => "Termination",
        ClauseCategory.Confidentiality => "Confidentiality",
        ClauseCategory.GoverningLaw => "Governing Law",
        ClauseCategory.IntellectualProperty => "Intellectual Property",
        ClauseCategory.Warranty => "Warranty",
        ClauseCategory.ForceMajeure => "Force Majeure",
        ClauseCategory.DisputeResolution => "Dispute Resolution",
        ClauseCategory.Assignment => "Assignment",
        ClauseCategory.DataProtection => "Data Protection",
        ClauseCategory.AutoRenewal => "Auto-Renewal",
        _ => throw new ArgumentOutOfRangeException(nameof(category), category, null),
    };
}
