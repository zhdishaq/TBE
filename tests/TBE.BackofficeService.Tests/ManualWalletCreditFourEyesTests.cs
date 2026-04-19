using System.Security.Claims;
using MassTransit;
using MassTransit.Testing;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using TBE.BackofficeService.Application.Controllers;
using TBE.BackofficeService.Application.Entities;
using TBE.BackofficeService.Infrastructure;
using TBE.Contracts.Events;
using Xunit;

namespace TBE.BackofficeService.Tests;

/// <summary>
/// D-39 — manual wallet credit (post-ticket refund via wallet) must use
/// the 4-eyes state machine (ops-finance opens, ops-admin approves,
/// self-approval forbidden). On approve PaymentService consumes
/// <c>WalletCreditApproved</c> and writes a <c>payment.WalletTransactions</c>
/// row of Kind=ManualCredit atomically; duplicate delivery is idempotent
/// via MassTransit inbox dedup. VALIDATION.md Task 6-01-04.
/// </summary>
public sealed class ManualWalletCreditFourEyesTests
{
    private static ControllerContext Ctx(string preferredUsername)
    {
        return new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(
                    new ClaimsIdentity(
                        new[] { new Claim("preferred_username", preferredUsername) },
                        "test")),
            },
        };
    }

    private static async Task<(WalletCreditRequestsController controller, BackofficeDbContext db, ITestHarness harness)> BuildAsync()
    {
        var provider = new ServiceCollection()
            .AddDbContext<BackofficeDbContext>(o => o.UseInMemoryDatabase($"wcreq-{Guid.NewGuid()}"))
            .AddSingleton(NullLoggerFactory.Instance)
            .AddLogging()
            .AddMassTransitTestHarness()
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BackofficeDbContext>();
        var controller = new WalletCreditRequestsController(db, harness.Bus, NullLogger<WalletCreditRequestsController>.Instance);
        return (controller, db, harness);
    }

    [Fact]
    [Trait("Category", "Phase06")]
    public async Task OpsFinance_opens_OpsAdmin_approves_publishes_WalletCreditApproved()
    {
        var (controller, db, harness) = await BuildAsync();
        try
        {
            var agencyId = Guid.NewGuid();

            // 1. ops-finance opens
            controller.ControllerContext = Ctx("ops-finance-1");
            var openBody = new WalletCreditRequestsController.CreateCreditReq
            {
                AgencyId = agencyId,
                Amount = 125.50m,
                Currency = "GBP",
                ReasonCode = "RefundedBooking",
                LinkedBookingId = Guid.NewGuid(),
                Notes = "Post-ticket refund: schedule cancellation by carrier",
            };
            var openResult = await controller.Open(openBody, CancellationToken.None);
            var accepted = Assert.IsType<AcceptedResult>(openResult);
            var createdId = (Guid)accepted.Value!.GetType().GetProperty("Id")!.GetValue(accepted.Value)!;
            var row = await db.WalletCreditRequests.SingleAsync();
            Assert.Equal("PendingApproval", row.Status);
            Assert.Equal("ops-finance-1", row.RequestedBy);
            Assert.Equal(125.50m, row.Amount);

            // 2. ops-admin approves
            controller.ControllerContext = Ctx("ops-admin-1");
            var approveBody = new WalletCreditRequestsController.ApproveCreditReq
            {
                ApprovalNotes = "Supplier ticket 990-XXX confirmed refunded",
            };
            var approveResult = await controller.Approve(createdId, approveBody, CancellationToken.None);
            Assert.IsType<NoContentResult>(approveResult);

            await db.Entry(row).ReloadAsync();
            Assert.Equal("Approved", row.Status);
            Assert.Equal("ops-admin-1", row.ApprovedBy);
            Assert.Equal("Supplier ticket 990-XXX confirmed refunded", row.ApprovalNotes);

            // 3. WalletCreditApproved published via test harness
            Assert.True(await harness.Published.Any<WalletCreditApproved>());
        }
        finally
        {
            await harness.Stop();
        }
    }

    [Fact]
    [Trait("Category", "Phase06")]
    public async Task Self_approval_returns_403_four_eyes_self_approval()
    {
        var (controller, db, harness) = await BuildAsync();
        try
        {
            controller.ControllerContext = Ctx("ops-admin-1");
            var open = await controller.Open(new WalletCreditRequestsController.CreateCreditReq
            {
                AgencyId = Guid.NewGuid(),
                Amount = 100m,
                Currency = "GBP",
                ReasonCode = "GoodwillCredit",
                Notes = "resolver rapport",
            }, CancellationToken.None);
            var createdId = (Guid)((AcceptedResult)open).Value!.GetType().GetProperty("Id")!.GetValue(((AcceptedResult)open).Value)!;

            // Same user tries to approve
            var result = await controller.Approve(createdId, new WalletCreditRequestsController.ApproveCreditReq
            {
                ApprovalNotes = "self-approve attempt",
            }, CancellationToken.None);

            var problem = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status403Forbidden, problem.StatusCode);
            var details = Assert.IsAssignableFrom<ProblemDetails>(problem.Value);
            Assert.Equal("/errors/four-eyes-self-approval", details.Type);

            var row = await db.WalletCreditRequests.SingleAsync();
            Assert.Equal("PendingApproval", row.Status);
            Assert.False(await harness.Published.Any<WalletCreditApproved>());
        }
        finally
        {
            await harness.Stop();
        }
    }

    [Fact]
    [Trait("Category", "Phase06")]
    public async Task Invalid_amount_returns_400_problem_with_allowed_range()
    {
        var (controller, db, harness) = await BuildAsync();
        try
        {
            controller.ControllerContext = Ctx("ops-finance-1");
            var result = await controller.Open(new WalletCreditRequestsController.CreateCreditReq
            {
                AgencyId = Guid.NewGuid(),
                Amount = 0m, // out of range
                Currency = "GBP",
                ReasonCode = "RefundedBooking",
                Notes = "x",
            }, CancellationToken.None);

            var problem = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
            var details = Assert.IsAssignableFrom<ProblemDetails>(problem.Value);
            Assert.Equal("/errors/wallet-credit-invalid-amount", details.Type);
            Assert.Empty(await db.WalletCreditRequests.ToListAsync());
        }
        finally
        {
            await harness.Stop();
        }
    }

    [Fact]
    [Trait("Category", "Phase06")]
    public async Task Invalid_reason_code_returns_400_problem()
    {
        var (controller, db, harness) = await BuildAsync();
        try
        {
            controller.ControllerContext = Ctx("ops-finance-1");
            var result = await controller.Open(new WalletCreditRequestsController.CreateCreditReq
            {
                AgencyId = Guid.NewGuid(),
                Amount = 50m,
                Currency = "GBP",
                ReasonCode = "NotInEnum",
                Notes = "x",
            }, CancellationToken.None);

            var problem = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
            var details = Assert.IsAssignableFrom<ProblemDetails>(problem.Value);
            Assert.Equal("/errors/wallet-credit-invalid-reason", details.Type);
        }
        finally
        {
            await harness.Stop();
        }
    }

    [Fact]
    [Trait("Category", "Phase06")]
    public async Task Expired_approval_returns_409_four_eyes_expired()
    {
        var (controller, db, harness) = await BuildAsync();
        try
        {
            var row = new WalletCreditRequest
            {
                Id = Guid.NewGuid(),
                AgencyId = Guid.NewGuid(),
                Amount = 200m,
                Currency = "GBP",
                ReasonCode = "DisputeResolution",
                Notes = "n",
                RequestedBy = "ops-finance-1",
                RequestedAt = DateTime.UtcNow.AddHours(-80),
                ExpiresAt = DateTime.UtcNow.AddHours(-1),
                Status = "PendingApproval",
            };
            db.WalletCreditRequests.Add(row);
            await db.SaveChangesAsync();

            controller.ControllerContext = Ctx("ops-admin-1");
            var result = await controller.Approve(row.Id, new WalletCreditRequestsController.ApproveCreditReq
            {
                ApprovalNotes = "late",
            }, CancellationToken.None);

            var problem = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status409Conflict, problem.StatusCode);
            var details = Assert.IsAssignableFrom<ProblemDetails>(problem.Value);
            Assert.Equal("/errors/four-eyes-expired", details.Type);
            await db.Entry(row).ReloadAsync();
            Assert.Equal("Expired", row.Status);
        }
        finally
        {
            await harness.Stop();
        }
    }

    [Fact]
    [Trait("Category", "Phase06")]
    public async Task ReasonCode_enum_values_all_accepted()
    {
        var (controller, db, harness) = await BuildAsync();
        try
        {
            var validCodes = new[] { "RefundedBooking", "GoodwillCredit", "DisputeResolution", "SupplierRefundPassthrough" };
            foreach (var code in validCodes)
            {
                controller.ControllerContext = Ctx("ops-finance-1");
                var result = await controller.Open(new WalletCreditRequestsController.CreateCreditReq
                {
                    AgencyId = Guid.NewGuid(),
                    Amount = 10m,
                    Currency = "GBP",
                    ReasonCode = code,
                    Notes = "test",
                }, CancellationToken.None);
                Assert.IsType<AcceptedResult>(result);
            }

            Assert.Equal(validCodes.Length, await db.WalletCreditRequests.CountAsync());
        }
        finally
        {
            await harness.Stop();
        }
    }
}
