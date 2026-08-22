using System.Diagnostics;
using ContractAI.API.Auth;
using ContractAI.API.Contracts;
using ContractAI.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ContractAI.API.Controllers;

[ApiController]
[Route("api/v1/search")]
public sealed class SearchController(
    IEmbeddingService embeddingService,
    IClauseQueryRepository clauseQueries,
    ICurrentTenant currentTenant) : ControllerBase
{
    private const int DefaultLimit = 10;
    private const int MaxLimit = 50;
    private const double DefaultThreshold = 0.7;

    [HttpPost("clauses")]
    [Authorize(Policy = AuthPolicies.Reader)]
    [ProducesResponseType(typeof(SearchClausesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SearchClauses(
        [FromBody] SearchClausesRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Query))
        {
            ModelState.AddModelError("query", "A non-empty query is required.");
        }

        if (request.SimilarityThreshold is { } threshold && (threshold < 0 || threshold > 1))
        {
            ModelState.AddModelError(
                "similarity_threshold", "similarity_threshold must be between 0 and 1.");
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        // Clamp rather than reject an out-of-range limit, matching the list endpoint;
        // the threshold defaults to a moderately strict cutoff when omitted.
        var similarityThreshold = request.SimilarityThreshold ?? DefaultThreshold;
        var limit = Math.Clamp(request.Limit ?? DefaultLimit, 1, MaxLimit);
        var tenantId = currentTenant.TenantId;

        var stopwatch = Stopwatch.StartNew();

        // The query embeds as a query and the stored clauses as documents; that
        // asymmetry is what the cosine distance is measured across.
        var queryEmbedding = await embeddingService.GenerateEmbeddingAsync(
            request.Query!, EmbeddingTaskType.RetrievalQuery, cancellationToken);

        var hits = await clauseQueries.SearchClausesAsync(
            queryEmbedding, tenantId, similarityThreshold, limit, cancellationToken);

        stopwatch.Stop();

        var results = hits
            .Select(h => new SearchClauseResult(
                h.ClauseId, h.ContractId, h.ContractFileName, h.ClauseType,
                h.RawText, h.SimilarityScore, h.PageNumber))
            .ToList();

        return Ok(new SearchClausesResponse(
            results, new SearchMeta(stopwatch.ElapsedMilliseconds, "cosine")));
    }
}
