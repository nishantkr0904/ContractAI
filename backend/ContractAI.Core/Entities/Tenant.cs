namespace ContractAI.Core.Entities;

// tenants: the multi-tenancy isolation root every other tenant-scoped row hangs off.
public class Tenant
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public bool IsDeleted { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public ICollection<User> Users { get; } = [];
    public ICollection<Contract> Contracts { get; } = [];
}
