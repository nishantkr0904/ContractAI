using System.Net.Http.Json;
using System.Text.Json.Serialization;
using ContractAI.Core.Interfaces;
using Microsoft.Extensions.Options;

namespace ContractAI.Services.Ai;

// Calls the Gemini embeddings REST API (generativelanguage.googleapis.com) through
// a typed HttpClient rather than a preview SDK: the call is a single POST, so a
// hand-rolled client keeps the dependency surface small and the marshalling
// explicit and testable.
public sealed class GoogleGenAiEmbeddingService : IEmbeddingService
{
    private readonly HttpClient http;
    private readonly GenAiOptions options;

    public GoogleGenAiEmbeddingService(HttpClient http, IOptions<GenAiOptions> options)
    {
        this.options = options.Value;
        this.http = http;
        http.BaseAddress = new Uri(this.options.BaseUrl, UriKind.Absolute);
        // The key travels as a header, not a query string, so it never lands in a
        // request log or a proxied URL.
        http.DefaultRequestHeaders.Add("X-goog-api-key", this.options.ApiKey);
    }

    public async Task<float[]> GenerateEmbeddingAsync(
        string text,
        EmbeddingTaskType taskType = EmbeddingTaskType.RetrievalDocument,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("Text to embed must not be empty.", nameof(text));
        }

        var request = new EmbedRequest(
            new EmbedContent([new EmbedPart(text)]),
            options.EmbeddingDimensions,
            ToApiTaskType(taskType));

        // The model is a path segment; embedContent (singular) returns one vector.
        using var response = await http.PostAsJsonAsync(
            $"models/{options.EmbeddingModel}:embedContent", request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException(
                $"Gemini embeddings request failed ({(int)response.StatusCode}): {body}");
        }

        var payload = await response.Content.ReadFromJsonAsync<EmbedResponse>(cancellationToken);
        var values = payload?.Embedding?.Values
            ?? throw new InvalidOperationException("Gemini embeddings response contained no vector.");

        if (values.Length != options.EmbeddingDimensions)
        {
            throw new InvalidOperationException(
                $"Expected a {options.EmbeddingDimensions}-D embedding but received {values.Length}.");
        }

        Normalize(values);
        return values;
    }

    // gemini-embedding-001 only returns unit vectors at its native 3072 dimensions;
    // a truncated (Matryoshka) output must be renormalized so cosine distance and
    // the caller's similarity threshold stay on a comparable 0..1 scale.
    private static void Normalize(float[] vector)
    {
        double sumSquares = 0;
        foreach (var value in vector)
        {
            sumSquares += (double)value * value;
        }

        var magnitude = Math.Sqrt(sumSquares);
        if (magnitude == 0)
        {
            return;
        }

        for (var i = 0; i < vector.Length; i++)
        {
            vector[i] = (float)(vector[i] / magnitude);
        }
    }

    private static string ToApiTaskType(EmbeddingTaskType taskType) => taskType switch
    {
        EmbeddingTaskType.RetrievalQuery => "RETRIEVAL_QUERY",
        _ => "RETRIEVAL_DOCUMENT",
    };

    private sealed record EmbedRequest(
        [property: JsonPropertyName("content")] EmbedContent Content,
        [property: JsonPropertyName("outputDimensionality")] int OutputDimensionality,
        [property: JsonPropertyName("taskType")] string TaskType);

    private sealed record EmbedContent(
        [property: JsonPropertyName("parts")] IReadOnlyList<EmbedPart> Parts);

    private sealed record EmbedPart(
        [property: JsonPropertyName("text")] string Text);

    private sealed record EmbedResponse(
        [property: JsonPropertyName("embedding")] EmbedValues? Embedding);

    private sealed record EmbedValues(
        [property: JsonPropertyName("values")] float[]? Values);
}
