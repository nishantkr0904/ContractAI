namespace ContractAI.Core.Entities;

// audit_logs: append-only compliance trail. Rows are never updated or deleted,
// so there is no updated_at and no navigation back from the actor.
public class AuditLog
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }

    // Null once the acting user is removed (user_id ON DELETE SET NULL).
    public Guid? UserId { get; set; }
    public string Action { get; set; } = null!;

    // jsonb documents held as raw JSON text; the shape varies by action, so
    // deserialization is the caller's concern.
    public string? OldData { get; set; }
    public string? NewData { get; set; }
    public DateTimeOffset Timestamp { get; set; }
}
