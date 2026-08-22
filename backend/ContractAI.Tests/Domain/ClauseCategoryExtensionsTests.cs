using ContractAI.Core.Enums;

namespace ContractAI.Tests.Domain;

// ToClauseTypeName is the natural key a parsed category is stored under in
// clause_types.name. A wrong label mis-files a clause; a colliding one merges two
// categories into a single row. Both mappings are pinned here.
public class ClauseCategoryExtensionsTests
{
    [Theory]
    [InlineData(ClauseCategory.Indemnification, "Indemnification")]
    [InlineData(ClauseCategory.PaymentTerms, "Payment Terms")]
    [InlineData(ClauseCategory.LimitationOfLiability, "Limitation of Liability")]
    [InlineData(ClauseCategory.Termination, "Termination")]
    [InlineData(ClauseCategory.Confidentiality, "Confidentiality")]
    [InlineData(ClauseCategory.GoverningLaw, "Governing Law")]
    [InlineData(ClauseCategory.IntellectualProperty, "Intellectual Property")]
    [InlineData(ClauseCategory.Warranty, "Warranty")]
    [InlineData(ClauseCategory.ForceMajeure, "Force Majeure")]
    [InlineData(ClauseCategory.DisputeResolution, "Dispute Resolution")]
    [InlineData(ClauseCategory.Assignment, "Assignment")]
    [InlineData(ClauseCategory.DataProtection, "Data Protection")]
    [InlineData(ClauseCategory.AutoRenewal, "Auto-Renewal")]
    public void ToClauseTypeName_ReturnsCanonicalLabel(ClauseCategory category, string expected)
    {
        Assert.Equal(expected, category.ToClauseTypeName());
    }

    // The enum is append-only C-ABI: a member added on the parser side but not given a
    // label here would throw at persistence time on the first contract that used it.
    // Walking every declared member catches that gap at test time instead.
    [Fact]
    public void ToClauseTypeName_MapsEveryDeclaredCategory()
    {
        foreach (var category in Enum.GetValues<ClauseCategory>())
        {
            Assert.False(string.IsNullOrWhiteSpace(category.ToClauseTypeName()));
        }
    }

    // clause_types.name is a natural key, so two categories sharing a label would be
    // stored as one row and become indistinguishable downstream.
    [Fact]
    public void ToClauseTypeName_ProducesDistinctLabels()
    {
        var labels = Enum.GetValues<ClauseCategory>()
            .Select(category => category.ToClauseTypeName())
            .ToList();

        Assert.Equal(labels.Count, labels.Distinct().Count());
    }

    // An ordinal outside the defined range (e.g. a parser build that emits a category
    // this build predates) must fail loudly rather than persist a bogus label.
    [Fact]
    public void ToClauseTypeName_UndefinedOrdinal_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ((ClauseCategory)99).ToClauseTypeName());
    }
}
