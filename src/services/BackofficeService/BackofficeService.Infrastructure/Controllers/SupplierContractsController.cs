using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TBE.BackofficeService.Application.Entities;
using TBE.BackofficeService.Infrastructure;

// Namespace kept as Application.Controllers per 06-01 precedent
// (DlqController + WalletCreditRequestsController). Physical project is
// Infrastructure because the controller depends on BackofficeDbContext.
namespace TBE.BackofficeService.Application.Controllers;

/// <summary>
/// Plan 06-02 Task 2 (BO-07) — supplier negotiated-rate contract CRUD.
///
/// <para>
/// Read (List + Get) is open to any ops-read role (BackofficeReadPolicy
/// at the class level). Mutations (Create / Update / Delete) require
/// ops-finance or ops-admin (BackofficeFinancePolicy at method level).
/// Delete is SOFT — sets <see cref="SupplierContract.IsDeleted"/> true
/// so the audit trail survives.
/// </para>
///
/// <para>
/// Status is a computed DTO field: <c>Upcoming</c> / <c>Active</c> /
/// <c>Expired</c> derived from <see cref="TimeProvider.GetUtcNow"/> vs
/// the row's ValidFrom/ValidTo (inclusive). The clock is injected so
/// tests can pin a deterministic "today" via
/// <c>Microsoft.Extensions.Time.Testing.FakeTimeProvider</c>.
/// </para>
///
/// <para>
/// RFC-7807 problem+json type URIs:
/// <list type="bullet">
///   <item>/errors/supplier-contract-invalid-validity (400)</item>
///   <item>/errors/supplier-contract-invalid-commission (400)</item>
///   <item>/errors/supplier-contract-invalid-product-type (400)</item>
///   <item>/errors/supplier-contract-invalid-net-rate (400)</item>
///   <item>/errors/supplier-contract-not-found (404)</item>
///   <item>/errors/missing-actor (401)</item>
/// </list>
/// </para>
/// </summary>
[ApiController]
[Route("api/backoffice/supplier-contracts")]
[Authorize(Policy = "BackofficeReadPolicy", AuthenticationSchemes = "Backoffice")]
public sealed class SupplierContractsController : ControllerBase
{
    private static readonly HashSet<string> ValidProductTypes = new(StringComparer.Ordinal)
    {
        "Flight", "Hotel", "Car", "Package",
    };

    private static readonly HashSet<string> ValidStatusFilters = new(StringComparer.Ordinal)
    {
        "Upcoming", "Active", "Expired",
    };

    private readonly BackofficeDbContext _db;
    private readonly TimeProvider _clock;
    private readonly ILogger<SupplierContractsController> _logger;

    public SupplierContractsController(
        BackofficeDbContext db,
        TimeProvider clock,
        ILogger<SupplierContractsController> logger)
    {
        _db = db;
        _clock = clock;
        _logger = logger;
    }

    // ───────────────────────── DTOs ─────────────────────────

    public sealed class CreateSupplierContractRequest
    {
        [Required]
        [StringLength(256, MinimumLength = 1)]
        public string SupplierName { get; set; } = string.Empty;

        [Required]
        [StringLength(32, MinimumLength = 1)]
        public string ProductType { get; set; } = string.Empty;

        public decimal NetRate { get; set; }
        public decimal CommissionPercent { get; set; }

        [Required]
        [StringLength(3, MinimumLength = 3)]
        public string Currency { get; set; } = "GBP";

        public DateTime ValidFrom { get; set; }
        public DateTime ValidTo { get; set; }

        [StringLength(2000)]
        public string Notes { get; set; } = string.Empty;
    }

    public sealed class UpdateSupplierContractRequest
    {
        [Required]
        [StringLength(256, MinimumLength = 1)]
        public string SupplierName { get; set; } = string.Empty;

        [Required]
        [StringLength(32, MinimumLength = 1)]
        public string ProductType { get; set; } = string.Empty;

        public decimal NetRate { get; set; }
        public decimal CommissionPercent { get; set; }

        [Required]
        [StringLength(3, MinimumLength = 3)]
        public string Currency { get; set; } = "GBP";

        public DateTime ValidFrom { get; set; }
        public DateTime ValidTo { get; set; }

        [StringLength(2000)]
        public string Notes { get; set; } = string.Empty;
    }

    public sealed class ListQuery
    {
        public string? ProductType { get; set; }
        public string? Status { get; set; }

        /// <summary>Free-text match on SupplierName (case-insensitive).</summary>
        public string? Q { get; set; }

        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }

    public sealed class SupplierContractRow
    {
        public Guid Id { get; set; }
        public string SupplierName { get; set; } = string.Empty;
        public string ProductType { get; set; } = string.Empty;
        public decimal NetRate { get; set; }
        public decimal CommissionPercent { get; set; }
        public string Currency { get; set; } = "GBP";
        public DateTime ValidFrom { get; set; }
        public DateTime ValidTo { get; set; }
        public string Notes { get; set; } = string.Empty;
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public string? UpdatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }

        /// <summary>Computed: Upcoming | Active | Expired (vs TimeProvider.GetUtcNow).</summary>
        public string Status { get; set; } = string.Empty;
    }

    public sealed class SupplierContractListResponse
    {
        public List<SupplierContractRow> Rows { get; set; } = new();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
    }

    // ─────────────────────── Endpoints ──────────────────────

    [HttpGet("")]
    public async Task<IActionResult> List(
        [FromQuery] ListQuery query,
        CancellationToken ct)
    {
        query ??= new ListQuery();
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize is < 1 or > 200 ? 20 : query.PageSize;

        var today = _clock.GetUtcNow().UtcDateTime;

        // Base filter: exclude soft-deleted.
        var q = _db.SupplierContracts.AsNoTracking().Where(r => !r.IsDeleted);

        if (!string.IsNullOrWhiteSpace(query.ProductType))
            q = q.Where(r => r.ProductType == query.ProductType);

        if (!string.IsNullOrWhiteSpace(query.Q))
        {
            var needle = query.Q.Trim();
            q = q.Where(r => EF.Functions.Like(r.SupplierName, $"%{needle}%"));
        }

        var total = await q.CountAsync(ct);

        // Materialize page BEFORE status filter — status is computed
        // client-side of DB, so we can't translate it to SQL. For full
        // fidelity we pull the page first; in practice the index on
        // (IsDeleted, ProductType, ValidTo) keeps this cheap.
        var rowsRaw = await q
            .OrderByDescending(r => r.ValidTo)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        IEnumerable<SupplierContractRow> mapped = rowsRaw.Select(r => new SupplierContractRow
        {
            Id = r.Id,
            SupplierName = r.SupplierName,
            ProductType = r.ProductType,
            NetRate = r.NetRate,
            CommissionPercent = r.CommissionPercent,
            Currency = r.Currency,
            ValidFrom = r.ValidFrom,
            ValidTo = r.ValidTo,
            Notes = r.Notes,
            CreatedBy = r.CreatedBy,
            CreatedAt = r.CreatedAt,
            UpdatedBy = r.UpdatedBy,
            UpdatedAt = r.UpdatedAt,
            Status = ComputeStatus(r.ValidFrom, r.ValidTo, today),
        });

        if (!string.IsNullOrWhiteSpace(query.Status) && ValidStatusFilters.Contains(query.Status))
            mapped = mapped.Where(r => r.Status == query.Status);

        var rows = mapped.ToList();

        return Ok(new SupplierContractListResponse
        {
            Rows = rows,
            TotalCount = total,
            Page = page,
            PageSize = pageSize,
        });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var row = await _db.SupplierContracts
            .AsNoTracking()
            .SingleOrDefaultAsync(r => r.Id == id, ct);

        if (row is null)
        {
            return Problem(
                statusCode: StatusCodes.Status404NotFound,
                type: "/errors/supplier-contract-not-found",
                title: "supplier_contract_not_found",
                detail: $"no supplier contract with id {id}");
        }

        var today = _clock.GetUtcNow().UtcDateTime;
        return Ok(new SupplierContractRow
        {
            Id = row.Id,
            SupplierName = row.SupplierName,
            ProductType = row.ProductType,
            NetRate = row.NetRate,
            CommissionPercent = row.CommissionPercent,
            Currency = row.Currency,
            ValidFrom = row.ValidFrom,
            ValidTo = row.ValidTo,
            Notes = row.Notes,
            CreatedBy = row.CreatedBy,
            CreatedAt = row.CreatedAt,
            UpdatedBy = row.UpdatedBy,
            UpdatedAt = row.UpdatedAt,
            Status = ComputeStatus(row.ValidFrom, row.ValidTo, today),
        });
    }

    [HttpPost("")]
    [Authorize(Policy = "BackofficeFinancePolicy", AuthenticationSchemes = "Backoffice")]
    public async Task<IActionResult> Create(
        [FromBody] CreateSupplierContractRequest req,
        CancellationToken ct)
    {
        var actor = User.FindFirst("preferred_username")?.Value;
        if (string.IsNullOrEmpty(actor))
        {
            return Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                type: "/errors/missing-actor",
                title: "missing_actor",
                detail: "missing preferred_username claim");
        }

        if (req is null)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                type: "/errors/supplier-contract-invalid-validity",
                title: "supplier_contract_invalid_body",
                detail: "request body required");
        }

        var validation = ValidateMutation(req.ProductType, req.NetRate, req.CommissionPercent, req.ValidFrom, req.ValidTo);
        if (validation is not null) return validation;

        var now = _clock.GetUtcNow().UtcDateTime;
        var row = new SupplierContract
        {
            Id = Guid.NewGuid(),
            SupplierName = req.SupplierName,
            ProductType = req.ProductType,
            NetRate = req.NetRate,
            CommissionPercent = req.CommissionPercent,
            Currency = req.Currency,
            ValidFrom = req.ValidFrom,
            ValidTo = req.ValidTo,
            Notes = req.Notes,
            CreatedBy = actor,
            CreatedAt = now,
            IsDeleted = false,
        };

        _db.SupplierContracts.Add(row);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "supplier-contract-created {ContractId} supplier={Supplier} product={Product} by={Actor}",
            row.Id, row.SupplierName, row.ProductType, actor);

        return CreatedAtAction(nameof(Get), new { id = row.Id }, new { Id = row.Id });
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "BackofficeFinancePolicy", AuthenticationSchemes = "Backoffice")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateSupplierContractRequest req,
        CancellationToken ct)
    {
        var actor = User.FindFirst("preferred_username")?.Value;
        if (string.IsNullOrEmpty(actor))
        {
            return Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                type: "/errors/missing-actor",
                title: "missing_actor",
                detail: "missing preferred_username claim");
        }

        if (req is null)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                type: "/errors/supplier-contract-invalid-validity",
                title: "supplier_contract_invalid_body",
                detail: "request body required");
        }

        var validation = ValidateMutation(req.ProductType, req.NetRate, req.CommissionPercent, req.ValidFrom, req.ValidTo);
        if (validation is not null) return validation;

        var row = await _db.SupplierContracts.SingleOrDefaultAsync(r => r.Id == id, ct);
        if (row is null || row.IsDeleted)
        {
            return Problem(
                statusCode: StatusCodes.Status404NotFound,
                type: "/errors/supplier-contract-not-found",
                title: "supplier_contract_not_found",
                detail: $"no supplier contract with id {id}");
        }

        row.SupplierName = req.SupplierName;
        row.ProductType = req.ProductType;
        row.NetRate = req.NetRate;
        row.CommissionPercent = req.CommissionPercent;
        row.Currency = req.Currency;
        row.ValidFrom = req.ValidFrom;
        row.ValidTo = req.ValidTo;
        row.Notes = req.Notes;
        row.UpdatedBy = actor;
        row.UpdatedAt = _clock.GetUtcNow().UtcDateTime;

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "supplier-contract-updated {ContractId} by={Actor}", row.Id, actor);

        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "BackofficeFinancePolicy", AuthenticationSchemes = "Backoffice")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var actor = User.FindFirst("preferred_username")?.Value;
        if (string.IsNullOrEmpty(actor))
        {
            return Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                type: "/errors/missing-actor",
                title: "missing_actor",
                detail: "missing preferred_username claim");
        }

        var row = await _db.SupplierContracts.SingleOrDefaultAsync(r => r.Id == id, ct);
        if (row is null || row.IsDeleted)
        {
            return Problem(
                statusCode: StatusCodes.Status404NotFound,
                type: "/errors/supplier-contract-not-found",
                title: "supplier_contract_not_found",
                detail: $"no supplier contract with id {id}");
        }

        row.IsDeleted = true;
        row.UpdatedBy = actor;
        row.UpdatedAt = _clock.GetUtcNow().UtcDateTime;
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "supplier-contract-soft-deleted {ContractId} by={Actor}", row.Id, actor);

        return NoContent();
    }

    // ─────────────────────── Helpers ────────────────────────

    private IActionResult? ValidateMutation(
        string productType,
        decimal netRate,
        decimal commissionPct,
        DateTime validFrom,
        DateTime validTo)
    {
        if (string.IsNullOrWhiteSpace(productType) || !ValidProductTypes.Contains(productType))
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                type: "/errors/supplier-contract-invalid-product-type",
                title: "supplier_contract_invalid_product_type",
                detail: $"ProductType must be one of: {string.Join(", ", ValidProductTypes)}");
        }

        if (netRate < 0)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                type: "/errors/supplier-contract-invalid-net-rate",
                title: "supplier_contract_invalid_net_rate",
                detail: "NetRate must be >= 0");
        }

        if (commissionPct < 0m || commissionPct > 100m)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                type: "/errors/supplier-contract-invalid-commission",
                title: "supplier_contract_invalid_commission",
                detail: "CommissionPercent must be in [0, 100]");
        }

        if (validTo < validFrom)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                type: "/errors/supplier-contract-invalid-validity",
                title: "supplier_contract_invalid_validity",
                detail: "ValidTo must be >= ValidFrom");
        }

        return null;
    }

    private static string ComputeStatus(DateTime validFrom, DateTime validTo, DateTime today)
    {
        if (today < validFrom) return "Upcoming";
        if (today > validTo) return "Expired";
        return "Active";
    }
}
