using ContractAI.Core.Enums;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;

namespace ContractAI.Data;

// Single place that teaches both layers about the project's PostgreSQL enums and
// the pgvector type. Both calls are required and must agree: the data source
// mapping drives ADO read/write serialization, while the EF provider mapping
// drives the model's store types. Configuring only one silently sends enums as
// integers (EF side) or fails to read them back (ADO side).
public static class ContractAiNpgsqlExtensions
{
    public static NpgsqlDataSourceBuilder ConfigureContractAi(this NpgsqlDataSourceBuilder builder)
    {
        builder.MapEnum<ContractStatus>("contract_status", UpperSnakeCaseNameTranslator.Instance);
        builder.MapEnum<RiskLevel>("risk_level", UpperSnakeCaseNameTranslator.Instance);
        builder.UseVector();
        return builder;
    }

    public static NpgsqlDbContextOptionsBuilder ConfigureContractAi(this NpgsqlDbContextOptionsBuilder options)
    {
        options.MapEnum<ContractStatus>("contract_status", nameTranslator: UpperSnakeCaseNameTranslator.Instance);
        options.MapEnum<RiskLevel>("risk_level", nameTranslator: UpperSnakeCaseNameTranslator.Instance);
        options.UseVector();
        return options;
    }
}
