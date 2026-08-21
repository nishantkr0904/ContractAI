using ContractAI.Core.Entities;
using ContractAI.Core.Enums;
using ContractAI.Core.Interfaces;
using ContractAI.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ContractAI.Services.Analysis;

// Reads the stored PDF for one contract, extracts its clauses through the native
// engine, and persists them. Runs inside a per-contract DI scope created by the
// worker, so the injected DbContext is private to this one contract.
public sealed class ContractAnalysisService(
    ContractDbContext db,
    IBlobStorageService blobStorage,
    IPdfTextExtractor textExtractor,
    IClauseParser clauseParser,
    ILogger<ContractAnalysisService> logger) : IContractAnalysisService
{
    public async Task ProcessContractAsync(Guid contractId, CancellationToken cancellationToken = default)
    {
        var contract = await db.Contracts.FirstOrDefaultAsync(c => c.Id == contractId, cancellationToken);
        if (contract is null)
        {
            // The row is created before the id is enqueued, so a miss means it was
            // hard-deleted between upload and processing — nothing left to analyze.
            logger.LogWarning("Contract {ContractId} not found for analysis; skipping.", contractId);
            return;
        }

        contract.Status = ContractStatus.Parsing;
        contract.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        try
        {
            var (extracted, clauses) = await ExtractAsync(contract, cancellationToken);
            await PersistClausesAsync(contract, extracted, clauses, cancellationToken);

            contract.Status = ContractStatus.ParsedSuccess;
            contract.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            // The contract's own status is how a failure surfaces to the client, so
            // it is recorded here rather than letting the exception bubble to the
            // worker as the only trace. The change tracker is cleared first so any
            // half-added clauses from the failed attempt are not saved alongside it.
            logger.LogError(e, "Analysis failed for contract {ContractId}", contractId);
            db.ChangeTracker.Clear();

            var toFail = await db.Contracts.FirstOrDefaultAsync(c => c.Id == contractId, cancellationToken);
            if (toFail is not null)
            {
                toFail.Status = ContractStatus.ParsedError;
                toFail.UpdatedAt = DateTimeOffset.UtcNow;
                await db.SaveChangesAsync(cancellationToken);
            }
        }
    }

    private async Task<(ExtractedText Extracted, IReadOnlyList<ParsedClause> Clauses)> ExtractAsync(
        Contract contract, CancellationToken cancellationToken)
    {
        var objectKey = BlobUri.ToObjectKey(contract.FileUri);
        await using var pdf = await blobStorage.DownloadAsync(objectKey, cancellationToken);

        var extracted = textExtractor.Extract(pdf);
        var clauses = clauseParser.Parse(extracted.Text);
        return (extracted, clauses);
    }

    private async Task PersistClausesAsync(
        Contract contract,
        ExtractedText extracted,
        IReadOnlyList<ParsedClause> clauses,
        CancellationToken cancellationToken)
    {
        if (clauses.Count == 0)
        {
            return;
        }

        var clauseTypeIds = await ResolveClauseTypeIdsAsync(clauses, cancellationToken);

        foreach (var clause in clauses)
        {
            db.ContractClauses.Add(new ContractClause
            {
                ContractId = contract.Id,
                ClauseTypeId = clauseTypeIds[clause.Category],
                RawText = clause.Text,
                PageNumber = extracted.PageNumberForByteOffset(clause.ByteOffset),
                ByteOffset = clause.ByteOffset,
                ConfidenceScore = clause.Confidence,
                // Embedding stays null until Phase 4 generates it.
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    // Get-or-create the clause_types rows the parsed categories map to. The worker
    // processes one contract at a time, so there is no concurrent writer racing the
    // insert of a shared type name.
    private async Task<Dictionary<ClauseCategory, Guid>> ResolveClauseTypeIdsAsync(
        IReadOnlyList<ParsedClause> clauses, CancellationToken cancellationToken)
    {
        var namesByCategory = clauses
            .Select(c => c.Category)
            .Distinct()
            .ToDictionary(c => c, c => c.ToClauseTypeName());
        var names = namesByCategory.Values.ToList();

        var existing = await db.ClauseTypes
            .Where(t => names.Contains(t.Name))
            .ToDictionaryAsync(t => t.Name, cancellationToken);

        foreach (var name in namesByCategory.Values)
        {
            if (!existing.ContainsKey(name))
            {
                var created = new ClauseType { Name = name };
                db.ClauseTypes.Add(created);
                existing[name] = created;
            }
        }

        await db.SaveChangesAsync(cancellationToken);

        return namesByCategory.ToDictionary(kvp => kvp.Key, kvp => existing[kvp.Value].Id);
    }
}
