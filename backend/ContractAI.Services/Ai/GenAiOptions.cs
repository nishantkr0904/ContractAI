using System.ComponentModel.DataAnnotations;

namespace ContractAI.Services.Ai;

// Binds the GenAi config section. ApiKey is the only secret and comes from user
// secrets / environment (never appsettings.json, which is committed); the rest have
// safe defaults so a fresh checkout runs without extra configuration.
public sealed class GenAiOptions
{
    public const string SectionName = "GenAi";

    [Required(AllowEmptyStrings = false)]
    public string ApiKey { get; init; } = string.Empty;

    [Required(AllowEmptyStrings = false)]
    public string BaseUrl { get; init; } = "https://generativelanguage.googleapis.com/v1beta/";

    // gemini-embedding-001 supports Matryoshka output truncation, so it can emit the
    // 1536 dimensions the schema's vector(1536) column expects; text-embedding-004
    // is fixed at 768 and would not fit the column.
    [Required(AllowEmptyStrings = false)]
    public string EmbeddingModel { get; init; } = "gemini-embedding-001";

    [Range(1, 3072)]
    public int EmbeddingDimensions { get; init; } = 1536;
}
