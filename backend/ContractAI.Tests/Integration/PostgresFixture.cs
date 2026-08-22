using ContractAI.Core.Interfaces;
using ContractAI.Data;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Testcontainers.PostgreSql;

namespace ContractAI.Tests.Integration;

// A disposable PostgreSQL 16 instance for exercising the Dapper read queries against a
// real server. The pgvector image is required rather than the stock postgres one:
// 01_init.sql creates the `vector` extension and an HNSW index, neither of which plain
// postgres can do.
//
// Composition goes through the production AddContractAiData rather than a hand-built
// data source, so these tests run against the same enum and pgvector mappings — and the
// same Dapper vector type handler — that the API composes at startup. A mapping that
// works here therefore works in the API.
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container =
        new PostgreSqlBuilder("pgvector/pgvector:pg16").Build();

    private ServiceProvider _provider = null!;

    public NpgsqlDataSource DataSource { get; private set; } = null!;

    public IClauseQueryRepository Repository => _provider.GetRequiredService<IClauseQueryRepository>();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        // The schema has to exist before the mapped data source opens its first
        // connection: ConfigureContractAi resolves the contract_status and risk_level
        // OIDs at that moment and fails if the types are not there yet. This bootstrap
        // connection is deliberately unmapped so it can run the DDL that creates them.
        await using (var bootstrap = new NpgsqlConnection(_container.GetConnectionString()))
        {
            await bootstrap.OpenAsync();
            await bootstrap.ExecuteAsync(await File.ReadAllTextAsync(SchemaPath));
        }

        _provider = new ServiceCollection()
            .AddContractAiData(_container.GetConnectionString())
            .BuildServiceProvider();

        DataSource = _provider.GetRequiredService<NpgsqlDataSource>();
    }

    public async Task DisposeAsync()
    {
        await _provider.DisposeAsync();
        await _container.DisposeAsync();
    }

    private static string SchemaPath =>
        Path.Combine(AppContext.BaseDirectory, "Schema", "01_init.sql");
}
