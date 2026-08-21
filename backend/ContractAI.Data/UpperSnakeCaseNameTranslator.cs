using Npgsql;
using Npgsql.NameTranslation;

namespace ContractAI.Data;

// Bridges PascalCase enum members to the UPPER_SNAKE_CASE labels declared in
// database/schema/01_init.sql (ParsedSuccess -> PARSED_SUCCESS). Npgsql ships a
// lowercase snake_case translator only, so the labels need this one.
//
// Used from three places that must agree on the same labels: HasPostgresEnum
// (DDL), the data source's MapEnum (ADO serialization), and the EF provider's
// MapEnum (model store type). ContractAiNpgsqlExtensions wires the latter two.
internal sealed class UpperSnakeCaseNameTranslator : INpgsqlNameTranslator
{
    public static readonly UpperSnakeCaseNameTranslator Instance = new();

    private static readonly NpgsqlSnakeCaseNameTranslator SnakeCase = new();

    public string TranslateTypeName(string clrName) =>
        SnakeCase.TranslateTypeName(clrName).ToUpperInvariant();

    public string TranslateMemberName(string clrName) =>
        SnakeCase.TranslateMemberName(clrName).ToUpperInvariant();
}
