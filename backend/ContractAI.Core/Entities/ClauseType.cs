namespace ContractAI.Core.Entities;

// clause_types: the lookup taxonomy that categorizes clauses. Rows are seeded
// from the parser's ClauseCategory vocabulary (see the KeywordTrie labels).
public class ClauseType
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }

    public ICollection<ContractClause> Clauses { get; } = [];
}
