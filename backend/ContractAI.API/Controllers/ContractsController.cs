using ContractAI.API.Auth;
using ContractAI.API.Contracts;
using ContractAI.Core.Entities;
using ContractAI.Core.Enums;
using ContractAI.Core.Interfaces;
using ContractAI.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ContractAI.API.Controllers;

[ApiController]
[Route("api/v1/contracts")]
public sealed class ContractsController(
    ContractDbContext db,
    IBlobStorageService blobStorage,
    IContractProcessingQueue processingQueue,
    IClauseQueryRepository clauseQueries,
    ICurrentTenant currentTenant) : ControllerBase
{
    private const long MaxUploadBytes = 50 * 1024 * 1024;
    private const int MaxPageSize = 100;

    [HttpPost("upload")]
    [Authorize(Policy = AuthPolicies.Writer)]
    [RequestSizeLimit(MaxUploadBytes)]
    [ProducesResponseType(typeof(ContractUploadResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Upload(IFormFile? file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return Problem("A non-empty file is required.", statusCode: StatusCodes.Status400BadRequest);
        }

        // Content type is client-supplied and easily spoofed, so it is a first
        // gate, not the authority; the extension is checked too. Deep validation
        // (magic bytes) is the parser's job downstream.
        var isPdf = file.ContentType == "application/pdf"
            || file.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase);
        if (!isPdf)
        {
            return Problem("Only PDF files are accepted.", statusCode: StatusCodes.Status400BadRequest);
        }

        var tenantId = currentTenant.TenantId;

        // The id is generated here rather than left to the database default so the
        // blob key can embed it before the row is inserted, keeping the stored
        // object and its contract row addressable by the same id.
        var contractId = Guid.NewGuid();
        var objectKey = $"{tenantId}/{contractId}/{Path.GetFileName(file.FileName)}";

        string fileUri;
        await using (var stream = file.OpenReadStream())
        {
            fileUri = await blobStorage.UploadAsync(stream, objectKey, "application/pdf", cancellationToken);
        }

        var contract = new Contract
        {
            Id = contractId,
            TenantId = tenantId,
            UploadedBy = currentTenant.UserId,
            FileName = file.FileName,
            FileUri = fileUri,
            Status = ContractStatus.Uploaded,
        };

        db.Contracts.Add(contract);
        await db.SaveChangesAsync(cancellationToken);

        // Enqueued only after the row is committed, so the worker can never dequeue
        // an id whose row is not yet visible.
        await processingQueue.EnqueueAsync(contractId, cancellationToken);

        var response = new ContractUploadResponse(
            contract.Id,
            contract.FileName,
            contract.Status,
            contract.CreatedAt,
            new ContractUploadLinks($"/api/v1/contracts/{contract.Id}"));

        return AcceptedAtAction(nameof(Upload), response);
    }

    [HttpGet]
    [Authorize(Policy = AuthPolicies.Reader)]
    [ProducesResponseType(typeof(PagedResponse<ContractSummaryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> List(
        [FromQuery] ContractListQuery query,
        CancellationToken cancellationToken)
    {
        // Filters are parsed here (not bound as enums) so an unrecognized value is a
        // 400 naming the offending field rather than a silently ignored parameter.
        ContractStatus? status = null;
        if (query.Status is not null)
        {
            if (!ApiEnum.TryParse<ContractStatus>(query.Status, out var parsed))
            {
                ModelState.AddModelError("status", $"'{query.Status}' is not a valid status.");
                return ValidationProblem(ModelState);
            }

            status = parsed;
        }

        RiskLevel? overallRisk = null;
        if (query.OverallRisk is not null)
        {
            if (!ApiEnum.TryParse<RiskLevel>(query.OverallRisk, out var parsed))
            {
                ModelState.AddModelError("overall_risk", $"'{query.OverallRisk}' is not a valid risk level.");
                return ValidationProblem(ModelState);
            }

            overallRisk = parsed;
        }

        if (!TryResolveSort(query.Sort, out var sortField, out var descending))
        {
            ModelState.AddModelError("sort", $"'{query.Sort}' is not a sortable field.");
            return ValidationProblem(ModelState);
        }

        // Clamp rather than reject: the spec gives a default and a max, so an
        // out-of-range page size is coerced into the allowed window.
        var page = Math.Max(query.Page, 1);
        var limit = Math.Clamp(query.Limit, 1, MaxPageSize);
        var tenantId = currentTenant.TenantId;

        // AsNoTracking: this is a read-only projection, so the change tracker would
        // only add overhead.
        var filtered = db.Contracts
            .AsNoTracking()
            .Where(c => c.TenantId == tenantId && !c.IsDeleted);

        if (status is { } s)
        {
            filtered = filtered.Where(c => c.Status == s);
        }

        if (overallRisk is { } r)
        {
            filtered = filtered.Where(c => c.OverallRisk == r);
        }

        var totalRecords = await filtered.CountAsync(cancellationToken);

        var items = await ApplySort(filtered, sortField, descending)
            .ThenBy(c => c.Id)
            .Skip((page - 1) * limit)
            .Take(limit)
            .Select(c => new ContractSummaryResponse(
                c.Id, c.FileName, c.FileUri, c.Status, c.OverallRisk, c.CreatedAt, c.UpdatedAt))
            .ToListAsync(cancellationToken);

        var totalPages = (int)Math.Ceiling(totalRecords / (double)limit);
        return Ok(new PagedResponse<ContractSummaryResponse>(
            items, new PaginationMeta(page, totalPages, totalRecords)));
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = AuthPolicies.Reader)]
    [ProducesResponseType(typeof(ContractDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var tenantId = currentTenant.TenantId;

        var contract = await db.Contracts
            .AsNoTracking()
            .Where(c => c.Id == id && c.TenantId == tenantId && !c.IsDeleted)
            .Select(c => new ContractDetailResponse(
                c.Id, c.UploadedBy, c.FileName, c.FileUri, c.Status, c.OverallRisk, c.CreatedAt, c.UpdatedAt))
            .FirstOrDefaultAsync(cancellationToken);

        // 404 (not 403) for a contract owned by another tenant, so the response does
        // not reveal that the id exists (API_REFERENCE.md, anti-enumeration).
        return contract is null
            ? Problem("Contract not found.", statusCode: StatusCodes.Status404NotFound)
            : Ok(contract);
    }

    [HttpGet("{id:guid}/file")]
    [Authorize(Policy = AuthPolicies.Reader)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetFile(Guid id, CancellationToken cancellationToken)
    {
        var tenantId = currentTenant.TenantId;

        // Only the columns the download needs, tenant-scoped: a foreign or unknown id
        // is a 404, so the blob is never fetched for a contract the caller cannot see.
        var file = await db.Contracts
            .AsNoTracking()
            .Where(c => c.Id == id && c.TenantId == tenantId && !c.IsDeleted)
            .Select(c => new { c.FileName, c.FileUri })
            .FirstOrDefaultAsync(cancellationToken);

        if (file is null)
        {
            return Problem("Contract not found.", statusCode: StatusCodes.Status404NotFound);
        }

        // Streamed straight from the object store rather than buffered: the PDF can be
        // tens of MB and the viewer only needs the bytes. The action result disposes
        // the stream after the response is written. No download filename is set so the
        // browser and react-pdf render it inline instead of forcing a save.
        var stream = await blobStorage.DownloadAsync(BlobUri.ToObjectKey(file.FileUri), cancellationToken);
        return File(stream, "application/pdf");
    }

    [HttpGet("{id:guid}/clauses")]
    [Authorize(Policy = AuthPolicies.Reader)]
    [ProducesResponseType(typeof(ClauseListResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetClauses(Guid id, CancellationToken cancellationToken)
    {
        var tenantId = currentTenant.TenantId;

        // Existence is checked separately so a real-but-empty clause set (a contract
        // still parsing, or one with no detected clauses) returns 200 with an empty
        // data array, while an unknown or foreign id returns 404.
        var exists = await db.Contracts
            .AsNoTracking()
            .AnyAsync(c => c.Id == id && c.TenantId == tenantId && !c.IsDeleted, cancellationToken);
        if (!exists)
        {
            return Problem("Contract not found.", statusCode: StatusCodes.Status404NotFound);
        }

        var clauses = await clauseQueries.GetByContractAsync(id, tenantId, cancellationToken);

        var data = clauses.Select(clause =>
        {
            var type = clause.ClauseType is null
                ? null
                : new ClauseTypeResponse(clause.ClauseType.Id, clause.ClauseType.Name, clause.ClauseType.Description);

            var score = clause.RiskScores.FirstOrDefault();
            var risk = score is null
                ? null
                : new ClauseRiskScoreResponse(score.Id, score.Severity, score.RuleViolated, score.Explanation);

            return new ClauseResponse(
                clause.Id, clause.ContractId, type, clause.RawText,
                clause.PageNumber, clause.ByteOffset, clause.ConfidenceScore, risk, clause.CreatedAt);
        }).ToList();

        return Ok(new ClauseListResponse(data));
    }

    // Sort spec is "field" ascending or "-field" descending (API_REFERENCE.md). The
    // field set is whitelisted so ordering can only touch indexed/known columns and
    // an unknown field is a client error rather than a silent no-op. A null/empty
    // sort falls back to newest-first, the natural default for a document list.
    private static bool TryResolveSort(string? sort, out string field, out bool descending)
    {
        if (string.IsNullOrWhiteSpace(sort))
        {
            field = "created_at";
            descending = true;
            return true;
        }

        descending = sort[0] == '-';
        field = (descending ? sort[1..] : sort).Trim();
        return field is "created_at" or "updated_at" or "file_name" or "status" or "overall_risk";
    }

    private static IOrderedQueryable<Contract> ApplySort(
        IQueryable<Contract> query, string field, bool descending) => field switch
    {
        "updated_at" => descending ? query.OrderByDescending(c => c.UpdatedAt) : query.OrderBy(c => c.UpdatedAt),
        "file_name" => descending ? query.OrderByDescending(c => c.FileName) : query.OrderBy(c => c.FileName),
        "status" => descending ? query.OrderByDescending(c => c.Status) : query.OrderBy(c => c.Status),
        "overall_risk" => descending ? query.OrderByDescending(c => c.OverallRisk) : query.OrderBy(c => c.OverallRisk),
        _ => descending ? query.OrderByDescending(c => c.CreatedAt) : query.OrderBy(c => c.CreatedAt),
    };
}
