using ContractAI.Core.Entities;
using ContractAI.Core.Enums;
using Microsoft.EntityFrameworkCore;
using Pgvector;

namespace ContractAI.Data;

public class ContractDbContext(DbContextOptions<ContractDbContext> options) : DbContext(options)
{
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Contract> Contracts => Set<Contract>();
    public DbSet<ClauseType> ClauseTypes => Set<ClauseType>();
    public DbSet<ContractClause> ContractClauses => Set<ContractClause>();
    public DbSet<ClauseRiskScore> ClauseRiskScores => Set<ClauseRiskScore>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        // Owned here rather than in the composition root so every consumer of the
        // context (API, tests, tooling) gets the same snake_case mapping without
        // having to remember to opt in. The provider/data source is still supplied
        // externally via UseNpgsql.
        optionsBuilder.UseSnakeCaseNamingConvention();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // The PostgreSQL enum types are declared in 01_init.sql; EF only needs to
        // know they exist so it maps the CLR enums instead of treating them as text.
        // The ADO layer needs the matching NpgsqlDataSourceBuilder.MapEnum calls.
        modelBuilder.HasPostgresEnum<ContractStatus>(
            schema: null, name: "contract_status", nameTranslator: UpperSnakeCaseNameTranslator.Instance);
        modelBuilder.HasPostgresEnum<RiskLevel>(
            schema: null, name: "risk_level", nameTranslator: UpperSnakeCaseNameTranslator.Instance);

        modelBuilder.Entity<Tenant>(entity =>
        {
            entity.Property(t => t.Id).HasDefaultValueSql("uuid_generate_v4()");
            entity.Property(t => t.IsDeleted).HasDefaultValue(false);
            entity.Property(t => t.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(t => t.UpdatedAt).HasDefaultValueSql("now()");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.Property(u => u.Id).HasDefaultValueSql("uuid_generate_v4()");
            entity.Property(u => u.IsActive).HasDefaultValue(true);
            entity.Property(u => u.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(u => u.UpdatedAt).HasDefaultValueSql("now()");
            entity.HasIndex(u => u.Email).IsUnique();

            entity.HasOne(u => u.Tenant)
                .WithMany(t => t.Users)
                .HasForeignKey(u => u.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Contract>(entity =>
        {
            entity.Property(c => c.Id).HasDefaultValueSql("uuid_generate_v4()");
            entity.Property(c => c.IsDeleted).HasDefaultValue(false);
            entity.Property(c => c.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(c => c.UpdatedAt).HasDefaultValueSql("now()");

            entity.HasOne(c => c.Tenant)
                .WithMany(t => t.Contracts)
                .HasForeignKey(c => c.TenantId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(c => c.Uploader)
                .WithMany()
                .HasForeignKey(c => c.UploadedBy)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<ClauseType>(entity =>
        {
            entity.Property(t => t.Id).HasDefaultValueSql("uuid_generate_v4()");
            entity.HasIndex(t => t.Name).IsUnique();
        });

        modelBuilder.Entity<ContractClause>(entity =>
        {
            entity.Property(c => c.Id).HasDefaultValueSql("uuid_generate_v4()");
            entity.Property(c => c.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(c => c.UpdatedAt).HasDefaultValueSql("now()");

            // Core stays free of the Pgvector dependency by holding the embedding as
            // float[]; the conversion to Pgvector's Vector happens here, at the
            // storage boundary. EF only invokes the converter for non-null values.
            entity.Property(c => c.Embedding)
                .HasColumnType("vector(1536)")
                .HasConversion(
                    v => new Vector(v!),
                    v => v.ToArray());

            entity.HasOne(c => c.Contract)
                .WithMany(c => c.Clauses)
                .HasForeignKey(c => c.ContractId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(c => c.ClauseType)
                .WithMany(t => t.Clauses)
                .HasForeignKey(c => c.ClauseTypeId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<ClauseRiskScore>(entity =>
        {
            entity.Property(s => s.Id).HasDefaultValueSql("uuid_generate_v4()");
            entity.Property(s => s.CreatedAt).HasDefaultValueSql("now()");

            entity.HasOne(s => s.Clause)
                .WithMany(c => c.RiskScores)
                .HasForeignKey(s => s.ContractClauseId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.Property(a => a.Id).HasDefaultValueSql("uuid_generate_v4()");
            entity.Property(a => a.Timestamp).HasDefaultValueSql("now()");
            entity.Property(a => a.OldData).HasColumnType("jsonb");
            entity.Property(a => a.NewData).HasColumnType("jsonb");

            // No navigations on AuditLog (append-only, no back-references), so the
            // FKs are declared without inverse collections.
            entity.HasOne<Tenant>()
                .WithMany()
                .HasForeignKey(a => a.TenantId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(a => a.UserId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }
}
