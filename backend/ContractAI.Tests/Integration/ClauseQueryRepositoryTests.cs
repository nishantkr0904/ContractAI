using ContractAI.Core.Enums;
using Dapper;
using Npgsql;
using Pgvector;

namespace ContractAI.Tests.Integration;

// Integration coverage for the two Dapper read queries in ClauseQueryRepository. Neither
// can be unit tested: their behaviour lives in SQL — a LATERAL "newest row" subquery, a
// tenant predicate that joins through contracts, and a pgvector cosine ordering — and no
// in-memory provider reproduces any of it.
//
// Every test generates its own tenant id, so rows one test seeds are invisible to the
// others' tenant-scoped queries and the shared container needs no reset between tests.
public class ClauseQueryRepositoryTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private const int EmbeddingDimensions = 1536;

    // Tenant scope is the security boundary: the SQL filters on contracts.tenant_id
    // rather than leaning on the RLS policy, which the owning role bypasses.
    [Fact]
    public async Task GetByContract_ContractOfAnotherTenant_ReturnsEmpty()
    {
        var owner = Guid.NewGuid();
        var intruder = Guid.NewGuid();
        var contractId = Guid.NewGuid();

        await using var connection = await fixture.DataSource.OpenConnectionAsync();
        await SeedTenantAsync(connection, owner);
        await SeedTenantAsync(connection, intruder);
        await SeedContractAsync(connection, contractId, owner);
        await SeedClauseAsync(connection, Guid.NewGuid(), contractId, byteOffset: 0);

        var clauses = await fixture.Repository.GetByContractAsync(contractId, intruder);

        Assert.Empty(clauses);
    }

    [Fact]
    public async Task GetByContract_SoftDeletedContract_ReturnsEmpty()
    {
        var tenantId = Guid.NewGuid();
        var contractId = Guid.NewGuid();

        await using var connection = await fixture.DataSource.OpenConnectionAsync();
        await SeedTenantAsync(connection, tenantId);
        await SeedContractAsync(connection, contractId, tenantId, isDeleted: true);
        await SeedClauseAsync(connection, Guid.NewGuid(), contractId, byteOffset: 0);

        var clauses = await fixture.Repository.GetByContractAsync(contractId, tenantId);

        Assert.Empty(clauses);
    }

    // The LATERAL subquery collapses a clause's assessment history to its newest row, so
    // an overridden clause reports one effective score rather than duplicating the clause
    // once per score.
    [Fact]
    public async Task GetByContract_ClauseWithSeveralRiskScores_ReturnsOnlyTheNewest()
    {
        var tenantId = Guid.NewGuid();
        var contractId = Guid.NewGuid();
        var clauseId = Guid.NewGuid();

        await using var connection = await fixture.DataSource.OpenConnectionAsync();
        await SeedTenantAsync(connection, tenantId);
        await SeedContractAsync(connection, contractId, tenantId);
        await SeedClauseAsync(connection, clauseId, contractId, byteOffset: 0);

        // Explicit, distinct timestamps: both rows would share created_at under the
        // column default, leaving the winner to the id tie-break and the test flaky.
        var recordedAt = DateTime.UtcNow;
        await SeedRiskScoreAsync(connection, clauseId, RiskLevel.Low, "AI-RULE-1", recordedAt.AddHours(-2));
        await SeedRiskScoreAsync(connection, clauseId, RiskLevel.Critical, "Human Override", recordedAt);

        var clauses = await fixture.Repository.GetByContractAsync(contractId, tenantId);

        var clause = Assert.Single(clauses);
        var score = Assert.Single(clause.RiskScores);
        Assert.Equal(RiskLevel.Critical, score.Severity);
        Assert.Equal("Human Override", score.RuleViolated);
    }

    // Clauses are returned in document order. byte_offset is nullable, and those clauses
    // sort last rather than first, which is what NULLS LAST buys.
    [Fact]
    public async Task GetByContract_OrdersByByteOffsetNullsLast_AndKeepsUnjoinedClauses()
    {
        var tenantId = Guid.NewGuid();
        var contractId = Guid.NewGuid();
        var clauseTypeId = Guid.NewGuid();
        var clauseTypeName = $"Termination-{Guid.NewGuid():N}";

        var unpositioned = Guid.NewGuid();
        var last = Guid.NewGuid();
        var first = Guid.NewGuid();

        await using var connection = await fixture.DataSource.OpenConnectionAsync();
        await SeedTenantAsync(connection, tenantId);
        await SeedContractAsync(connection, contractId, tenantId);
        await SeedClauseTypeAsync(connection, clauseTypeId, clauseTypeName);

        // Seeded out of order so the assertion cannot pass on insertion order alone.
        await SeedClauseAsync(connection, unpositioned, contractId, byteOffset: null);
        await SeedClauseAsync(connection, last, contractId, clauseTypeId, byteOffset: 100);
        await SeedClauseAsync(connection, first, contractId, byteOffset: 10);

        var clauses = await fixture.Repository.GetByContractAsync(contractId, tenantId);

        Assert.Equal(new[] { first, last, unpositioned }, clauses.Select(clause => clause.Id).ToArray());

        // A clause with neither a type nor a score still comes back: both joins are outer.
        Assert.Null(clauses[0].ClauseType);
        Assert.Empty(clauses[0].RiskScores);
        Assert.Equal(clauseTypeName, clauses[1].ClauseType!.Name);
    }

    // Nearest-first ordering, the similarity threshold, NULL embeddings and tenant scope
    // are all one WHERE/ORDER BY away from each other, so they are asserted together.
    [Fact]
    public async Task SearchClauses_RanksByCosineSimilarity_AndAppliesThreshold()
    {
        var tenantId = Guid.NewGuid();
        var corpus = await SeedSearchCorpusAsync(tenantId);

        var results = await fixture.Repository.SearchClausesAsync(
            Embedding((0, 1f)), tenantId, similarityThreshold: 0.5, limit: 10);

        // The orthogonal clause (similarity 0), the unembedded clause and the other
        // tenant's identical clause are all absent.
        Assert.Equal(new[] { corpus.Aligned, corpus.Oblique }, results.Select(result => result.ClauseId).ToArray());
        Assert.Equal(1.0, results[0].SimilarityScore, precision: 4);
        Assert.Equal(0.7071, results[1].SimilarityScore, precision: 4);
        Assert.Equal(corpus.FileName, results[0].ContractFileName);
    }

    [Fact]
    public async Task SearchClauses_Limit_CapsResultsKeepingNearestFirst()
    {
        var tenantId = Guid.NewGuid();
        var corpus = await SeedSearchCorpusAsync(tenantId);

        var results = await fixture.Repository.SearchClausesAsync(
            Embedding((0, 1f)), tenantId, similarityThreshold: 0.5, limit: 1);

        var hit = Assert.Single(results);
        Assert.Equal(corpus.Aligned, hit.ClauseId);
    }

    // Four clauses for the tenant plus one for a foreign tenant. Against the query
    // [1,0,0,...]: Aligned scores 1.0, Oblique 1/sqrt(2) ~ 0.7071, and Orthogonal 0.0.
    private async Task<SearchCorpus> SeedSearchCorpusAsync(Guid tenantId)
    {
        var contractId = Guid.NewGuid();
        var fileName = $"msa-{Guid.NewGuid():N}.pdf";
        var aligned = Guid.NewGuid();
        var oblique = Guid.NewGuid();

        await using var connection = await fixture.DataSource.OpenConnectionAsync();
        await SeedTenantAsync(connection, tenantId);
        await SeedContractAsync(connection, contractId, tenantId, fileName: fileName);

        await SeedClauseAsync(connection, aligned, contractId, byteOffset: 0, embedding: Embedding((0, 1f)));
        await SeedClauseAsync(connection, oblique, contractId, byteOffset: 1, embedding: Embedding((0, 1f), (1, 1f)));
        await SeedClauseAsync(connection, Guid.NewGuid(), contractId, byteOffset: 2, embedding: Embedding((1, 1f)));
        await SeedClauseAsync(connection, Guid.NewGuid(), contractId, byteOffset: 3);

        var foreignTenantId = Guid.NewGuid();
        var foreignContractId = Guid.NewGuid();
        await SeedTenantAsync(connection, foreignTenantId);
        await SeedContractAsync(connection, foreignContractId, foreignTenantId);
        await SeedClauseAsync(
            connection, Guid.NewGuid(), foreignContractId, byteOffset: 0, embedding: Embedding((0, 1f)));

        return new SearchCorpus(aligned, oblique, fileName);
    }

    private sealed record SearchCorpus(Guid Aligned, Guid Oblique, string FileName);

    // vector(1536) is a fixed width, so every stored embedding and every query has to be
    // exactly that long. Only the leading components are set, which keeps the cosine
    // arithmetic in the tests exact and readable.
    private static float[] Embedding(params (int Index, float Value)[] components)
    {
        var values = new float[EmbeddingDimensions];
        foreach (var (index, value) in components)
        {
            values[index] = value;
        }

        return values;
    }

    private static Task SeedTenantAsync(NpgsqlConnection connection, Guid tenantId) =>
        connection.ExecuteAsync(
            "INSERT INTO tenants (id, name) VALUES (@tenantId, @name);",
            new { tenantId, name = $"tenant-{tenantId:N}" });

    // Enum columns are written as text and cast: Dapper would otherwise send a CLR enum
    // as its underlying int, which contract_status and risk_level do not accept.
    private static Task SeedContractAsync(
        NpgsqlConnection connection,
        Guid contractId,
        Guid tenantId,
        string? fileName = null,
        bool isDeleted = false) =>
        connection.ExecuteAsync(
            """
            INSERT INTO contracts
                (id, tenant_id, file_name, file_uri, status, overall_risk, is_deleted)
            VALUES
                (@contractId, @tenantId, @fileName, @fileUri,
                 CAST(@status AS contract_status), CAST(@risk AS risk_level), @isDeleted);
            """,
            new
            {
                contractId,
                tenantId,
                fileName = fileName ?? $"contract-{contractId:N}.pdf",
                fileUri = $"s3://contracts/{contractId:N}.pdf",
                status = "PARSED_SUCCESS",
                risk = "UNKNOWN",
                isDeleted,
            });

    private static Task SeedClauseTypeAsync(NpgsqlConnection connection, Guid clauseTypeId, string name) =>
        connection.ExecuteAsync(
            "INSERT INTO clause_types (id, name, description) VALUES (@clauseTypeId, @name, NULL);",
            new { clauseTypeId, name });

    private static Task SeedClauseAsync(
        NpgsqlConnection connection,
        Guid clauseId,
        Guid contractId,
        Guid? clauseTypeId = null,
        int? byteOffset = null,
        float[]? embedding = null)
    {
        var parameters = new DynamicParameters(new
        {
            clauseId,
            contractId,
            clauseTypeId,
            rawText = $"Clause body {clauseId:N}",
            pageNumber = 1,
            byteOffset,
            confidence = 0.9d,
        });

        // An unembedded clause omits the column rather than binding a null vector: the
        // Dapper vector handler only converts non-null Vector values.
        if (embedding is not null)
        {
            parameters.Add("embedding", new Vector(embedding));
        }

        var columns = embedding is null ? string.Empty : ", embedding";
        var values = embedding is null ? string.Empty : ", @embedding";

        return connection.ExecuteAsync(
            $"""
             INSERT INTO contract_clauses
                 (id, contract_id, clause_type_id, raw_text, page_number, byte_offset, confidence_score{columns})
             VALUES
                 (@clauseId, @contractId, @clauseTypeId, @rawText, @pageNumber, @byteOffset, @confidence{values});
             """,
            parameters);
    }

    private static Task SeedRiskScoreAsync(
        NpgsqlConnection connection,
        Guid clauseId,
        RiskLevel severity,
        string ruleViolated,
        DateTime createdAt) =>
        connection.ExecuteAsync(
            """
            INSERT INTO clause_risk_scores
                (id, contract_clause_id, severity, rule_violated, explanation, created_at)
            VALUES
                (@id, @clauseId, CAST(@severity AS risk_level), @ruleViolated, @explanation, @createdAt);
            """,
            new
            {
                id = Guid.NewGuid(),
                clauseId,
                severity = severity.ToString().ToUpperInvariant(),
                ruleViolated,
                explanation = "Seeded for integration test.",
                createdAt,
            });
}
