using System.Text.Json;
using ContractAI.API.Auth;
using ContractAI.API.Contracts;
using ContractAI.Core.Entities;
using ContractAI.Core.Enums;
using ContractAI.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ContractAI.API.Controllers;

[ApiController]
[Route("api/v1/clauses")]
public sealed class ClausesController(ContractDbContext db, ICurrentTenant currentTenant) : ControllerBase
{
    private const string OverrideAction = "CLAUSE_RISK_OVERRIDE";
    private const string OverrideMarker = "Human Override";
    private const string OverrideSuffix = " (Human Override)";
    private const int MaxRuleViolatedLength = 255;

    // Audit payloads mirror the API wire form (snake_case keys, UPPER_SNAKE_CASE
    // enum values) so the trail reads the same as the responses that produced it.
    private static readonly JsonSerializerOptions AuditJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper) },
    };

    [HttpPatch("{id:guid}/risk")]
    [Authorize(Policy = AuthPolicies.Writer)]
    [ProducesResponseType(typeof(RiskOverrideResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> OverrideRisk(
        Guid id,
        [FromBody] RiskOverrideRequest request,
        CancellationToken cancellationToken)
    {
        // Unknown is the enum's zero value, so it is also what a missing/omitted
        // severity binds to; either way it is not a valid override target.
        if (request.Severity == RiskLevel.Unknown)
        {
            ModelState.AddModelError("severity", "A severity of LOW, MEDIUM, HIGH, or CRITICAL is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Explanation))
        {
            ModelState.AddModelError("explanation", "An explanation is required.");
        }
        else if (request.Explanation.Length > RiskOverrideRequest.MaxExplanationLength)
        {
            ModelState.AddModelError(
                "explanation",
                $"An explanation must be at most {RiskOverrideRequest.MaxExplanationLength} characters.");
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var tenantId = currentTenant.TenantId;

        // Tenant scope is enforced through the owning contract, so a clause from
        // another tenant is indistinguishable from one that does not exist.
        var clauseExists = await db.ContractClauses
            .AsNoTracking()
            .AnyAsync(
                c => c.Id == id && c.Contract.TenantId == tenantId && !c.Contract.IsDeleted,
                cancellationToken);
        if (!clauseExists)
        {
            return Problem("Clause not found.", statusCode: StatusCodes.Status404NotFound);
        }

        // The newest existing assessment, if any: its rule label is carried forward
        // (marked as overridden) and it is recorded as the audit's old value.
        var prior = await db.ClauseRiskScores
            .AsNoTracking()
            .Where(s => s.ContractClauseId == id)
            .OrderByDescending(s => s.CreatedAt)
            .ThenByDescending(s => s.Id)
            .FirstOrDefaultAsync(cancellationToken);

        var score = new ClauseRiskScore
        {
            Id = Guid.NewGuid(),
            ContractClauseId = id,
            Severity = request.Severity,
            RuleViolated = BuildRuleViolated(prior?.RuleViolated),
            Explanation = request.Explanation,
        };

        var audit = new AuditLog
        {
            TenantId = tenantId,
            UserId = currentTenant.UserId,
            Action = OverrideAction,
            OldData = prior is null ? null : Serialize(prior.Severity, prior.RuleViolated, prior.Explanation),
            NewData = Serialize(score.Severity, score.RuleViolated, score.Explanation),
        };

        // One SaveChanges: EF wraps the new score and its audit row in a single
        // transaction, so an override is never recorded without its audit trail.
        db.ClauseRiskScores.Add(score);
        db.AuditLogs.Add(audit);
        await db.SaveChangesAsync(cancellationToken);

        return Ok(new RiskOverrideResponse(
            score.Id, score.ContractClauseId, score.Severity, score.RuleViolated, score.Explanation, score.CreatedAt));
    }

    // Carry the prior rule label forward with an "overridden" marker so the history
    // stays legible. Idempotent on the marker so repeated overrides do not stack it
    // (this also covers a clause that had no prior AI score), and truncated to the
    // column width.
    private static string BuildRuleViolated(string? priorRule)
    {
        if (string.IsNullOrEmpty(priorRule))
        {
            return OverrideMarker;
        }

        if (priorRule.EndsWith(OverrideMarker, StringComparison.Ordinal))
        {
            return priorRule;
        }

        var composed = priorRule + OverrideSuffix;
        return composed.Length <= MaxRuleViolatedLength
            ? composed
            : composed[..MaxRuleViolatedLength];
    }

    private static string Serialize(RiskLevel severity, string ruleViolated, string explanation) =>
        JsonSerializer.Serialize(
            new { severity, rule_violated = ruleViolated, explanation }, AuditJsonOptions);
}
