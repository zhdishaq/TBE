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
using TBE.BookingService.Application;
using TBE.Contracts.Events;
using Xunit;

namespace TBE.BackofficeService.Tests;

/// <summary>
/// BO-03 — staff cancel/modify must use the 4-eyes state machine
/// (ops-cs opens, ops-admin approves, self-approval forbidden, expiry
/// enforced). Approval writes one BookingEvents row and publishes
/// <c>BookingCancellationApproved</c> via the EF outbox. VALIDATION.md
/// Task 6-01-03.
/// </summary>
public sealed class StaffCancelModifyTests
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

    private static async Task<(StaffBookingActionsController controller, BackofficeDbContext db, ITestHarness harness, RecordingEventsWriter writer)> BuildAsync()
    {
        var provider = new ServiceCollection()
            .AddDbContext<BackofficeDbContext>(o => o.UseInMemoryDatabase($"cancel-{Guid.NewGuid()}"))
            .AddSingleton(NullLoggerFactory.Instance)
            .AddLogging()
            .AddMassTransitTestHarness()
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BackofficeDbContext>();
        var writer = new RecordingEventsWriter();
        var controller = new StaffBookingActionsController(db, harness.Bus, writer, NullLogger<StaffBookingActionsController>.Instance);
        return (controller, db, harness, writer);
    }

    [Fact]
    [Trait("Category", "Phase06")]
    public async Task OpsCs_can_open_request_OpsAdmin_approves_logged_in_BookingEvents_BO03()
    {
        var (controller, db, harness, writer) = await BuildAsync();
        try
        {
            var bookingId = Guid.NewGuid();

            // 1. ops-cs opens
            controller.ControllerContext = Ctx("ops-cs-1");
            var openBody = new StaffBookingActionsController.CancelBookingReq
            {
                ReasonCode = "CustomerRequest",
                Reason = "Customer asked to cancel via phone",
            };
            var openResult = await controller.Open(bookingId, openBody, CancellationToken.None);
            var accepted = Assert.IsType<AcceptedResult>(openResult);
            var createdId = Assert.IsType<Guid>(accepted.Value!.GetType().GetProperty("Id")!.GetValue(accepted.Value));
            var row = await db.CancellationRequests.SingleAsync();
            Assert.Equal("PendingApproval", row.Status);
            Assert.Equal("ops-cs-1", row.RequestedBy);
            Assert.True(row.ExpiresAt > row.RequestedAt);

            // 2. ops-admin (different user) approves
            controller.ControllerContext = Ctx("ops-admin-1");
            var approveBody = new StaffBookingActionsController.ApproveCancelReq
            {
                ApprovalReason = "Verified customer identity; no fraud risk",
            };
            var approveResult = await controller.Approve(createdId, approveBody, CancellationToken.None);
            Assert.IsType<NoContentResult>(approveResult);

            await db.Entry(row).ReloadAsync();
            Assert.Equal("Approved", row.Status);
            Assert.Equal("ops-admin-1", row.ApprovedBy);
            Assert.NotNull(row.ApprovedAt);

            // 3. BookingEvents audit row written via writer
            Assert.Single(writer.Events);
            Assert.Equal(bookingId, writer.Events[0].BookingId);
            Assert.Equal("BookingCancellationApproved", writer.Events[0].EventType);
            Assert.Equal("ops-admin-1", writer.Events[0].Actor);

            // 4. BookingCancellationApproved published via test harness
            Assert.True(await harness.Published.Any<BookingCancellationApproved>());
        }
        finally
        {
            await harness.Stop();
        }
    }

    [Fact]
    [Trait("Category", "Phase06")]
    public async Task Self_approval_returns_403_problem_four_eyes_self_approval()
    {
        var (controller, db, harness, writer) = await BuildAsync();
        try
        {
            var bookingId = Guid.NewGuid();

            controller.ControllerContext = Ctx("ops-admin-1");
            var open = await controller.Open(bookingId, new StaffBookingActionsController.CancelBookingReq
            {
                ReasonCode = "Other",
                Reason = "Internal ops cancellation for test",
            }, CancellationToken.None);
            var accepted = Assert.IsType<AcceptedResult>(open);
            var createdId = (Guid)accepted.Value!.GetType().GetProperty("Id")!.GetValue(accepted.Value)!;

            // Same user tries to approve
            var result = await controller.Approve(createdId, new StaffBookingActionsController.ApproveCancelReq
            {
                ApprovalReason = "n/a",
            }, CancellationToken.None);

            var problem = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status403Forbidden, problem.StatusCode);
            var details = Assert.IsAssignableFrom<ProblemDetails>(problem.Value);
            Assert.Equal("/errors/four-eyes-self-approval", details.Type);

            var row = await db.CancellationRequests.SingleAsync();
            Assert.Equal("PendingApproval", row.Status); // unchanged
            Assert.Empty(writer.Events);
        }
        finally
        {
            await harness.Stop();
        }
    }

    [Fact]
    [Trait("Category", "Phase06")]
    public async Task Approval_after_expiry_returns_409_four_eyes_expired()
    {
        var (controller, db, harness, writer) = await BuildAsync();
        try
        {
            var bookingId = Guid.NewGuid();

            // Seed directly with rewound ExpiresAt.
            var row = new CancellationRequest
            {
                Id = Guid.NewGuid(),
                BookingId = bookingId,
                ReasonCode = "FraudSuspected",
                Reason = "AVS mismatch + velocity flag",
                RequestedBy = "ops-cs-1",
                RequestedAt = DateTime.UtcNow.AddHours(-80),
                ExpiresAt = DateTime.UtcNow.AddHours(-1), // already expired
                Status = "PendingApproval",
            };
            db.CancellationRequests.Add(row);
            await db.SaveChangesAsync();

            controller.ControllerContext = Ctx("ops-admin-1");
            var result = await controller.Approve(row.Id, new StaffBookingActionsController.ApproveCancelReq
            {
                ApprovalReason = "late review",
            }, CancellationToken.None);

            var problem = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status409Conflict, problem.StatusCode);
            var details = Assert.IsAssignableFrom<ProblemDetails>(problem.Value);
            Assert.Equal("/errors/four-eyes-expired", details.Type);

            await db.Entry(row).ReloadAsync();
            Assert.Equal("Expired", row.Status); // flipped as a side-effect of expiry detection
            Assert.Empty(writer.Events);
        }
        finally
        {
            await harness.Stop();
        }
    }

    [Fact]
    [Trait("Category", "Phase06")]
    public async Task Approval_of_already_decided_returns_409_already_decided()
    {
        var (controller, db, harness, writer) = await BuildAsync();
        try
        {
            var row = new CancellationRequest
            {
                Id = Guid.NewGuid(),
                BookingId = Guid.NewGuid(),
                ReasonCode = "DuplicateBooking",
                Reason = "Booking reference collision",
                RequestedBy = "ops-cs-1",
                RequestedAt = DateTime.UtcNow.AddMinutes(-10),
                ExpiresAt = DateTime.UtcNow.AddHours(71),
                Status = "Approved",
                ApprovedBy = "ops-admin-1",
                ApprovedAt = DateTime.UtcNow.AddMinutes(-5),
            };
            db.CancellationRequests.Add(row);
            await db.SaveChangesAsync();

            controller.ControllerContext = Ctx("ops-admin-2");
            var result = await controller.Approve(row.Id, new StaffBookingActionsController.ApproveCancelReq
            {
                ApprovalReason = "double-check",
            }, CancellationToken.None);

            var problem = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status409Conflict, problem.StatusCode);
            var details = Assert.IsAssignableFrom<ProblemDetails>(problem.Value);
            Assert.Equal("/errors/four-eyes-already-decided", details.Type);
            Assert.Empty(writer.Events);
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
        var (controller, db, harness, _) = await BuildAsync();
        try
        {
            controller.ControllerContext = Ctx("ops-cs-1");
            var result = await controller.Open(Guid.NewGuid(), new StaffBookingActionsController.CancelBookingReq
            {
                ReasonCode = "NotAnEnumValue",
                Reason = "something",
            }, CancellationToken.None);

            var problem = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
            var details = Assert.IsAssignableFrom<ProblemDetails>(problem.Value);
            Assert.Equal("/errors/cancellation-invalid-reason", details.Type);
            Assert.Empty(await db.CancellationRequests.ToListAsync());
        }
        finally
        {
            await harness.Stop();
        }
    }

    /// <summary>
    /// Test double for <see cref="IBookingEventsWriter"/> — records every
    /// call so assertions can verify the approval flow wrote exactly one
    /// BookingEvents row with the correct EventType + Actor.
    /// </summary>
    private sealed class RecordingEventsWriter : IBookingEventsWriter
    {
        public List<(Guid BookingId, string EventType, string Actor, Guid CorrelationId, object Snapshot)> Events { get; } = new();

        public Task WriteAsync(
            Guid bookingId,
            string eventType,
            string actor,
            Guid correlationId,
            object snapshotPayload,
            CancellationToken ct)
        {
            Events.Add((bookingId, eventType, actor, correlationId, snapshotPayload));
            return Task.CompletedTask;
        }
    }
}
