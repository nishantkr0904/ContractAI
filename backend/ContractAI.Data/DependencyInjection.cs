using ContractAI.Core.Interfaces;
using ContractAI.Data.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace ContractAI.Data;

public static class DependencyInjection
{
    // One entry point so the composition root cannot register the DbContext and the
    // Dapper data source with divergent enum/vector mappings. The single
    // NpgsqlDataSource is shared: EF Core reads/writes through it, and the Dapper
    // repositories open their connections from the same pool.
    public static IServiceCollection AddContractAiData(
        this IServiceCollection services, string connectionString)
    {
        services.AddSingleton(_ =>
            new NpgsqlDataSourceBuilder(connectionString).ConfigureContractAi().Build());

        services.AddDbContext<ContractDbContext>((sp, options) =>
            options.UseNpgsql(
                sp.GetRequiredService<NpgsqlDataSource>(),
                npgsql => npgsql.ConfigureContractAi()));

        services.AddScoped<IClauseQueryRepository, ClauseQueryRepository>();

        return services;
    }
}
