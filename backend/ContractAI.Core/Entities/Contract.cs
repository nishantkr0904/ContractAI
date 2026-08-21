using ContractAI.Core.Enums;

namespace ContractAI.Core.Entities;

// contracts: document metadata for a single uploaded file. The PDF bytes live in
// blob storage; file_uri points at them.
public class Contract
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }

    // Null once the uploading user is removed (uploaded_by ON DELETE SET NULL).
    public Guid? UploadedBy { get; set; }
    public string FileName { get; set; } = null!;
    public string FileUri { get; set; } = null!;
    public ContractStatus Status { get; set; }
    public RiskLevel OverallRisk { get; set; }
    public bool IsDeleted { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Tenant Tenant { get; set; } = null!;
    public User? Uploader { get; set; }
    public ICollection<ContractClause> Clauses { get; } = [];
}
