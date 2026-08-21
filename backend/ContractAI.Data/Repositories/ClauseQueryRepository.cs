using ContractAI.Core.Entities;
using ContractAI.Core.Enums;
using ContractAI.Core.Interfaces;
using Dapper;
using Npgsql;

namespace ContractAI.Data.Repositories;

public sealed class ClauseQueryRepository(NpgsqlDataSource dataSource) : IClauseQueryRepository
{
    // The tenant predicate joins through contracts rather than trusting the RLS
    // policy on its own: RLS is bypassed for the table owner, which is the role
    // used in local development.
    //
    // severity is read as text so the query does not depend on Npgsql enum
    // mappings being registered on the injected data source.
    //
    // The LATERAL subquery keeps this to one row per clause, so no grouping pass
    // is needed after the read.
    private const string GetByContractSql = """
        SELECT
            c.id               AS Id,
            c.contract_id      AS ContractId,
            c.clause_type_id   AS ClauseTypeId,
            c.raw_text         AS RawText,
            c.page_number      AS PageNumber,
            c.byte_offset      AS ByteOffset,
            c.confidence_score AS ConfidenceScore,
            c.created_at       AS CreatedAt,
            c.updated_at       AS UpdatedAt,
            t.name             AS ClauseTypeName,
            t.description      AS ClauseTypeDescription,
            r.id               AS RiskScoreId,
            r.severity::text   AS RiskSeverity,
            r.rule_violated    AS RiskRuleViolated,
            r.explanation      AS RiskExplanation,
            r.created_at       AS RiskCreatedAt
        FROM contract_clauses c
        JOIN contracts ct
          ON ct.id = c.contract_id
        LEFT JOIN clause_types t
          ON t.id = c.clause_type_id
        LEFT JOIN LATERAL (
            SELECT s.id, s.severity, s.rule_violated, s.explanation, s.created_at
            FROM clause_risk_scores s
            WHERE s.contract_clause_id = c.id
            ORDER BY s.created_at DESC, s.id DESC
            LIMIT 1
        ) r ON TRUE
        WHERE c.contract_id = @ContractId
          AND ct.tenant_id = @TenantId
          AND ct.is_deleted IS NOT TRUE
        ORDER BY c.byte_offset NULLS LAST, c.created_at, c.id;
        """;

    public async Task<IReadOnlyList<ContractClause>> GetByContractAsync(
        Guid contractId,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        var rows = await connection.QueryAsync<ClauseRow>(
            new CommandDefinition(
                GetByContractSql,
                new { ContractId = contractId, TenantId = tenantId },
                cancellationToken: cancellationToken));

        return rows.Select(ToEntity).ToList();
    }

    private static ContractClause ToEntity(ClauseRow row)
    {
        var clause = new ContractClause
        {
            Id = row.Id,
            ContractId = row.ContractId,
            ClauseTypeId = row.ClauseTypeId,
            RawText = row.RawText,
            PageNumber = row.PageNumber,
            ByteOffset = row.ByteOffset,
            ConfidenceScore = row.ConfidenceScore,
            CreatedAt = row.CreatedAt,
            UpdatedAt = row.UpdatedAt,
        };

        if (row.ClauseTypeId is { } clauseTypeId)
        {
            clause.ClauseType = new ClauseType
            {
                Id = clauseTypeId,
                Name = row.ClauseTypeName!,
                Description = row.ClauseTypeDescription,
            };
        }

        if (row.RiskScoreId is { } riskScoreId)
        {
            clause.RiskScores.Add(new ClauseRiskScore
            {
                Id = riskScoreId,
                ContractClauseId = row.Id,
                Severity = Enum.Parse<RiskLevel>(row.RiskSeverity!, ignoreCase: true),
                RuleViolated = row.RiskRuleViolated!,
                Explanation = row.RiskExplanation!,
                CreatedAt = row.RiskCreatedAt!.Value,
                Clause = clause,
            });
        }

        return clause;
    }

    // Field types mirror exactly what Npgsql's reader returns so Dapper's
    // (type-strict) constructor matching accepts this constructor. In particular
    // timestamptz surfaces as DateTime (Kind=Utc), not DateTimeOffset; ToEntity
    // relies on the implicit DateTime->DateTimeOffset conversion, which yields a
    // +00:00 offset for a UTC value.
    private sealed record ClauseRow(
        Guid Id,
        Guid ContractId,
        Guid? ClauseTypeId,
        string RawText,
        int? PageNumber,
        int? ByteOffset,
        double? ConfidenceScore,
        DateTime CreatedAt,
        DateTime UpdatedAt,
        string? ClauseTypeName,
        string? ClauseTypeDescription,
        Guid? RiskScoreId,
        string? RiskSeverity,
        string? RiskRuleViolated,
        string? RiskExplanation,
        DateTime? RiskCreatedAt);
}
