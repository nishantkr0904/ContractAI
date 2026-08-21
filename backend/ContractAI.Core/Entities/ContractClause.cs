namespace ContractAI.Core.Entities;

// contract_clauses: one extracted clause span. page_number and byte_offset are
// nullable because the native parser sees a flat text buffer and cannot resolve
// PDF pagination; the service layer fills page_number from its offset table.
public class ContractClause
{
    public Guid Id { get; set; }
    public Guid ContractId { get; set; }

    // Null once a clause type is removed (clause_type_id ON DELETE SET NULL).
    public Guid? ClauseTypeId { get; set; }
    public string RawText { get; set; } = null!;
    public int? PageNumber { get; set; }
    public int? ByteOffset { get; set; }
    public double? ConfidenceScore { get; set; }

    // The vector(1536) embedding, held as float[] so Core stays free of the
    // Pgvector dependency; ContractAI.Data converts it at the storage boundary.
    // Populated in Phase 4, so null until a clause has been embedded.
    public float[]? Embedding { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Contract Contract { get; set; } = null!;
    public ClauseType? ClauseType { get; set; }
    public ICollection<ClauseRiskScore> RiskScores { get; } = [];
}
