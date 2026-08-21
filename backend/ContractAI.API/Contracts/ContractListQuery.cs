using Microsoft.AspNetCore.Mvc;

namespace ContractAI.API.Contracts;

// Query parameters for GET /contracts. Filters arrive as raw strings and are
// parsed in the controller so an unrecognized value can be reported as a field
// validation error rather than being silently dropped. Explicit binding names keep
// the ModelState keys snake_case, so a 400's error keys match the wire contract
// (ModelState is case-insensitive and would otherwise keep the PascalCase property
// name).
public sealed class ContractListQuery
{
    public int Page { get; init; } = 1;
    public int Limit { get; init; } = 20;

    [FromQuery(Name = "sort")]
    public string? Sort { get; init; }

    [FromQuery(Name = "status")]
    public string? Status { get; init; }

    [FromQuery(Name = "overall_risk")]
    public string? OverallRisk { get; init; }
}

internal static class ApiEnum
{
    // Query-string enum values are UPPER_SNAKE_CASE ("PARSED_SUCCESS") to match the
    // JSON wire form, while the CLR members are PascalCase. Dropping the underscores
    // lets a case-insensitive parse line the two up without a per-enum lookup table.
    public static bool TryParse<TEnum>(string value, out TEnum result) where TEnum : struct, Enum
        => Enum.TryParse(value.Replace("_", string.Empty), ignoreCase: true, out result)
           && Enum.IsDefined(result);
}
