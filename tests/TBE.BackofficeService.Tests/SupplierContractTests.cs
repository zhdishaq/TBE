using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using TBE.BackofficeService.Application.Controllers;
using TBE.BackofficeService.Application.Entities;
using TBE.BackofficeService.Infrastructure;
using Xunit;

namespace TBE.BackofficeService.Tests;

/// <summary>
/// BO-07 — Supplier contract CRUD with validity-window status chip
/// (Upcoming / Active / Expired). ops-finance + ops-admin can mutate;
/// ops-read + ops-cs can only read. Soft-delete via IsDeleted flag
/// preserves audit.
///
/// <para>
/// Status is a computed DTO field derived server-side from a fixed
/// <see cref="FakeTimeProvider"/> today vs the contract's ValidFrom /
/// ValidTo (inclusive). All tests pin the clock so the three buckets
/// are deterministic.
/// </para>
/// </summary>
public sealed class SupplierContractTests
{
    private static readonly DateTime TodayFixed = new(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc);

    private static ControllerContext Ctx(string preferredUsername, params string[] roles)
    {
        var claims = roles.Select(r => new Claim(ClaimTypes.Role, r)).ToList();
        claims.Add(new Claim("preferred_username", preferredUsername));
        return new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")),
            },
        };
    }

    private static (SupplierContractsController controller, BackofficeDbContext db, FakeTimeProvider clock) Build()
    {
        var clock = new FakeTimeProvider(TodayFixed);
        var provider = new ServiceCollection()
            .AddDbContext<BackofficeDbContext>(o => o.UseInMemoryDatabase($"suppliers-{Guid.NewGuid()}"))
            .AddSingleton<TimeProvider>(clock)
            .BuildServiceProvider(validateScopes: false);

        var db = provider.GetRequiredService<BackofficeDbContext>();
        var controller = new SupplierContractsController(
            db, clock, NullLogger<SupplierContractsController>.Instance);
        return (controller, db, clock);
    }

    private static SupplierContractsController.CreateSupplierContractRequest ValidReq(
        string supplier = "Airline X",
        string productType = "Flight",
        decimal netRate = 450m,
        decimal commissionPct = 12.5m,
        DateTime? validFrom = null,
        DateTime? validTo = null) => new()
    {
        SupplierName = supplier,
        ProductType = productType,
        NetRate = netRate,
        CommissionPercent = commissionPct,
        Currency = "GBP",
        ValidFrom = validFrom ?? new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc),
        ValidTo = validTo ?? new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc),
        Notes = "Negotiated seat deal",
    };

    [Fact]
    [Trait("Category", "Phase06")]
    public async Task ops_finance_can_POST_contract_returns_201_with_id()
    {
        var (controller, db, _) = Build();
        controller.ControllerContext = Ctx("ops-finance-1", "ops-finance");
        var result = await controller.Create(ValidReq(), CancellationToken.None);

        var accepted = Assert.IsType<CreatedAtActionResult>(result);
        var body = accepted.Value!;
        var id = (Guid)body.GetType().GetProperty("Id")!.GetValue(body)!;

        var row = await db.SupplierContracts.SingleAsync(r => r.Id == id);
        Assert.Equal("Airline X", row.SupplierName);
        Assert.Equal("Flight", row.ProductType);
        Assert.Equal(450m, row.NetRate);
        Assert.Equal(12.5m, row.CommissionPercent);
        Assert.Equal("ops-finance-1", row.CreatedBy);
        Assert.False(row.IsDeleted);
    }

    [Fact]
    [Trait("Category", "Phase06")]
    public async Task GET_list_returns_computed_status_from_today_vs_validity()
    {
        var (controller, db, _) = Build();

        db.SupplierContracts.AddRange(
            new SupplierContract
            {
                Id = Guid.NewGuid(),
                SupplierName = "A",
                ProductType = "Flight",
                NetRate = 1,
                CommissionPercent = 10,
                Currency = "GBP",
                ValidFrom = TodayFixed.AddDays(-30),
                ValidTo = TodayFixed.AddDays(-1),
                CreatedBy = "seed",
                CreatedAt = TodayFixed,
            }, // Expired
            new SupplierContract
            {
                Id = Guid.NewGuid(),
                SupplierName = "B",
                ProductType = "Hotel",
                NetRate = 2,
                CommissionPercent = 10,
                Currency = "GBP",
                ValidFrom = TodayFixed.AddDays(-1),
                ValidTo = TodayFixed.AddDays(30),
                CreatedBy = "seed",
                CreatedAt = TodayFixed,
            }, // Active
            new SupplierContract
            {
                Id = Guid.NewGuid(),
                SupplierName = "C",
                ProductType = "Car",
                NetRate = 3,
                CommissionPercent = 10,
                Currency = "GBP",
                ValidFrom = TodayFixed.AddDays(10),
                ValidTo = TodayFixed.AddDays(100),
                CreatedBy = "seed",
                CreatedAt = TodayFixed,
            }); // Upcoming
        await db.SaveChangesAsync();

        controller.ControllerContext = Ctx("ops-read-1", "ops-read");
        var result = await controller.List(new SupplierContractsController.ListQuery(), CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<SupplierContractsController.SupplierContractListResponse>(ok.Value);

        Assert.Equal(3, body.Rows.Count);
        Assert.Contains(body.Rows, r => r.Status == "Expired");
        Assert.Contains(body.Rows, r => r.Status == "Active");
        Assert.Contains(body.Rows, r => r.Status == "Upcoming");
    }

    [Fact]
    [Trait("Category", "Phase06")]
    public async Task GET_list_filters_by_productType_and_status()
    {
        var (controller, db, _) = Build();

        db.SupplierContracts.AddRange(
            new SupplierContract
            {
                Id = Guid.NewGuid(),
                SupplierName = "A",
                ProductType = "Flight",
                NetRate = 1,
                CommissionPercent = 10,
                Currency = "GBP",
                ValidFrom = TodayFixed.AddDays(-1),
                ValidTo = TodayFixed.AddDays(30),
                CreatedBy = "seed",
                CreatedAt = TodayFixed,
            },
            new SupplierContract
            {
                Id = Guid.NewGuid(),
                SupplierName = "B",
                ProductType = "Hotel",
                NetRate = 2,
                CommissionPercent = 10,
                Currency = "GBP",
                ValidFrom = TodayFixed.AddDays(-1),
                ValidTo = TodayFixed.AddDays(30),
                CreatedBy = "seed",
                CreatedAt = TodayFixed,
            });
        await db.SaveChangesAsync();

        controller.ControllerContext = Ctx("ops-read-1", "ops-read");
        var result = await controller.List(
            new SupplierContractsController.ListQuery
            {
                ProductType = "Hotel",
                Status = "Active",
            },
            CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<SupplierContractsController.SupplierContractListResponse>(ok.Value);

        Assert.Single(body.Rows);
        Assert.Equal("Hotel", body.Rows[0].ProductType);
        Assert.Equal("Active", body.Rows[0].Status);
    }

    [Fact]
    [Trait("Category", "Phase06")]
    public async Task PUT_updates_stamps_UpdatedBy_and_UpdatedAt()
    {
        var (controller, db, clock) = Build();
        var row = new SupplierContract
        {
            Id = Guid.NewGuid(),
            SupplierName = "A",
            ProductType = "Flight",
            NetRate = 100,
            CommissionPercent = 10,
            Currency = "GBP",
            ValidFrom = TodayFixed,
            ValidTo = TodayFixed.AddMonths(3),
            CreatedBy = "seed",
            CreatedAt = TodayFixed.AddDays(-1),
        };
        db.SupplierContracts.Add(row);
        await db.SaveChangesAsync();

        clock.Advance(TimeSpan.FromMinutes(5));
        controller.ControllerContext = Ctx("ops-finance-1", "ops-finance");
        var req = new SupplierContractsController.UpdateSupplierContractRequest
        {
            SupplierName = "A - Updated",
            ProductType = "Flight",
            NetRate = 125,
            CommissionPercent = 11,
            Currency = "GBP",
            ValidFrom = row.ValidFrom,
            ValidTo = row.ValidTo,
            Notes = "Re-negotiated",
        };
        var result = await controller.Update(row.Id, req, CancellationToken.None);
        Assert.IsType<NoContentResult>(result);

        await db.Entry(row).ReloadAsync();
        Assert.Equal("A - Updated", row.SupplierName);
        Assert.Equal(125, row.NetRate);
        Assert.Equal("ops-finance-1", row.UpdatedBy);
        Assert.NotNull(row.UpdatedAt);
    }

    [Fact]
    [Trait("Category", "Phase06")]
    public async Task DELETE_soft_deletes_flag_IsDeleted()
    {
        var (controller, db, _) = Build();
        var row = new SupplierContract
        {
            Id = Guid.NewGuid(),
            SupplierName = "A",
            ProductType = "Flight",
            NetRate = 100,
            CommissionPercent = 10,
            Currency = "GBP",
            ValidFrom = TodayFixed,
            ValidTo = TodayFixed.AddMonths(3),
            CreatedBy = "seed",
            CreatedAt = TodayFixed,
        };
        db.SupplierContracts.Add(row);
        await db.SaveChangesAsync();

        controller.ControllerContext = Ctx("ops-finance-1", "ops-finance");
        var result = await controller.Delete(row.Id, CancellationToken.None);
        Assert.IsType<NoContentResult>(result);

        await db.Entry(row).ReloadAsync();
        Assert.True(row.IsDeleted);

        // List excludes soft-deleted by default
        var listResult = await controller.List(new SupplierContractsController.ListQuery(), CancellationToken.None);
        var body = Assert.IsType<SupplierContractsController.SupplierContractListResponse>(
            ((OkObjectResult)listResult).Value);
        Assert.Empty(body.Rows);
    }

    [Fact]
    [Trait("Category", "Phase06")]
    public async Task POST_with_validFrom_after_validTo_returns_400_problem()
    {
        var (controller, _, _) = Build();
        controller.ControllerContext = Ctx("ops-finance-1", "ops-finance");
        var req = ValidReq(
            validFrom: new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc),
            validTo: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var result = await controller.Create(req, CancellationToken.None);

        var problem = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
        var details = Assert.IsAssignableFrom<ProblemDetails>(problem.Value);
        Assert.Equal("/errors/supplier-contract-invalid-validity", details.Type);
    }

    [Fact]
    [Trait("Category", "Phase06")]
    public async Task POST_with_commission_out_of_range_returns_400_problem()
    {
        var (controller, _, _) = Build();
        controller.ControllerContext = Ctx("ops-finance-1", "ops-finance");
        var req = ValidReq(commissionPct: 150m);
        var result = await controller.Create(req, CancellationToken.None);

        var problem = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
        var details = Assert.IsAssignableFrom<ProblemDetails>(problem.Value);
        Assert.Equal("/errors/supplier-contract-invalid-commission", details.Type);
    }

    [Fact]
    [Trait("Category", "Phase06")]
    public async Task POST_with_invalid_product_type_returns_400_problem()
    {
        var (controller, _, _) = Build();
        controller.ControllerContext = Ctx("ops-finance-1", "ops-finance");
        var req = ValidReq(productType: "NotInEnum");
        var result = await controller.Create(req, CancellationToken.None);

        var problem = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
        var details = Assert.IsAssignableFrom<ProblemDetails>(problem.Value);
        Assert.Equal("/errors/supplier-contract-invalid-product-type", details.Type);
    }

    [Fact]
    [Trait("Category", "Phase06")]
    public async Task GET_not_found_returns_404()
    {
        var (controller, _, _) = Build();
        controller.ControllerContext = Ctx("ops-read-1", "ops-read");
        var result = await controller.Get(Guid.NewGuid(), CancellationToken.None);

        var problem = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status404NotFound, problem.StatusCode);
    }
}
