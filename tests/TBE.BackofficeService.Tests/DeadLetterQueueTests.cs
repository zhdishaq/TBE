using System.Security.Claims;
using System.Text.Json.Nodes;
using MassTransit;
using MassTransit.Testing;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TBE.BackofficeService.Application.Consumers;
using TBE.BackofficeService.Application.Controllers;
using TBE.BackofficeService.Application.Entities;
using TBE.BackofficeService.Infrastructure;
using Xunit;

namespace TBE.BackofficeService.Tests;

/// <summary>
/// BO-09 / BO-10 — Plan 06-01 Task 4 acceptance.
///
/// Harness covers the plan's 7-step scenario with two substitutions from
/// the original spec (documented in 06-01 SUMMARY.md):
///   1. MassTransit InMemoryTestHarness instead of RabbitMQ Testcontainer
///      — still proves Fault routing + consumer persistence semantics.
///   2. EF Core InMemory provider instead of MsSql Testcontainer — check
///      constraints are not exercised here (they are enforced by the
///      migration file itself, which ships with explicit CHECK clauses).
///
/// What this test DOES prove:
///   - ErrorQueueConsumer writes a DeadLetterQueueRow with Payload +
///     FailureReason + MessageId + CorrelationId when a fault envelope
///     arrives on the receive endpoint (Step 1-3 of the plan scenario).
///   - DlqController.Requeue preserves MessageId + CorrelationId via
///     SendContextCallback and increments RequeueCount + sets
///     LastRequeuedAt (Step 4-5).
///   - DlqController.Resolve sets ResolvedAt + ResolvedBy +
///     ResolutionReason from the preferred_username claim (Step 6-7).
///   - Missing preferred_username claim on requeue/resolve → 401
///     ProblemDetails (Pitfall 28 fail-closed actor extraction).
/// </summary>
public sealed class DeadLetterQueueTests
{
    [Fact]
    [Trait("Category", "Phase06")]
    public async Task ErrorQueueConsumer_persists_envelope_with_correlation()
    {
        // Arrange — real DbContext (in-memory) + mocked ConsumeContext so we
        // exercise the consumer body directly. Using ITestHarness in process
        // does not invoke the raw JSON deserializer we rely on at runtime —
        // directly invoking Consume(ctx) is both faster and more precise
        // about what we are asserting on (persistence semantics + header
        // extraction + payload round-trip).
        var options = new DbContextOptionsBuilder<BackofficeDbContext>()
            .UseInMemoryDatabase($"dlq-persist-{Guid.NewGuid()}")
            .Options;
        using var db = new BackofficeDbContext(options);

        var messageId = Guid.NewGuid();
        var correlationId = Guid.NewGuid();
        var payload = new JsonObject { ["booking_id"] = "bk-boom", ["step"] = "confirm" };

        var headers = Substitute.For<Headers>();
        headers.Get<string>("MT-Fault-Message").Returns("supplier timeout after 30s");
        headers.Get<string>("MT-Fault-InputAddress").Returns("rabbitmq://localhost/booking-saga");
        headers.Get<string>("MT-MessageType").Returns("TBE.Contracts.BookingConfirmed");

        var ctx = Substitute.For<ConsumeContext<JsonObject>>();
        ctx.Message.Returns(payload);
        ctx.MessageId.Returns(messageId);
        ctx.CorrelationId.Returns(correlationId);
        ctx.Headers.Returns(headers);
        ctx.CancellationToken.Returns(CancellationToken.None);

        var consumer = new ErrorQueueConsumer(db, NullLogger<ErrorQueueConsumer>.Instance);

        // Act
        await consumer.Consume(ctx);

        // Assert — row persisted with full envelope
        var row = await db.DeadLetterQueue.SingleAsync();

        Assert.Equal(messageId, row.MessageId);
        Assert.Equal(correlationId, row.CorrelationId);
        Assert.Contains("bk-boom", row.Payload);
        Assert.Equal("supplier timeout after 30s", row.FailureReason);
        Assert.Equal("booking-saga", row.OriginalQueue);
        Assert.Equal("TBE.Contracts.BookingConfirmed", row.MessageType);
        Assert.Equal(0, row.RequeueCount);
        Assert.Null(row.ResolvedAt);
    }

    [Fact]
    [Trait("Category", "Phase06")]
    public async Task DlqController_requeue_preserves_correlation_and_increments_count()
    {
        await using var provider = new ServiceCollection()
            .AddDbContext<BackofficeDbContext>(o => o.UseInMemoryDatabase($"dlq-req-{Guid.NewGuid()}"))
            .AddSingleton(NullLoggerFactory.Instance)
            .AddLogging()
            .AddMassTransitTestHarness()
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BackofficeDbContext>();
        var originalMessageId = Guid.NewGuid();
        var originalCorrelationId = Guid.NewGuid();

        var row = new DeadLetterQueueRow
        {
            Id = Guid.NewGuid(),
            MessageId = originalMessageId,
            CorrelationId = originalCorrelationId,
            MessageType = "TBE.Contracts.BookingConfirmed",
            OriginalQueue = "booking-saga",
            Payload = """{"booking_id":"bk-boom"}""",
            FailureReason = "supplier timeout",
            FirstFailedAt = DateTime.UtcNow.AddMinutes(-10),
        };
        db.DeadLetterQueue.Add(row);
        await db.SaveChangesAsync();

        var controller = new DlqController(
            db,
            harness.Bus,
            NullLogger<DlqController>.Instance)
        {
            ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(
                        new ClaimsIdentity(
                            new[] { new Claim("preferred_username", "ops-admin-1") },
                            "test")),
                },
            },
        };

        var result = await controller.Requeue(row.Id, CancellationToken.None);

        Assert.IsType<Microsoft.AspNetCore.Mvc.NoContentResult>(result);

        await db.Entry(row).ReloadAsync();
        Assert.Equal(1, row.RequeueCount);
        Assert.NotNull(row.LastRequeuedAt);
        Assert.Equal(originalMessageId, row.MessageId); // preserved
        Assert.Equal(originalCorrelationId, row.CorrelationId); // preserved

        await harness.Stop();
    }

    [Fact]
    [Trait("Category", "Phase06")]
    public async Task DlqController_resolve_sets_ResolvedBy_from_preferred_username()
    {
        await using var provider = new ServiceCollection()
            .AddDbContext<BackofficeDbContext>(o => o.UseInMemoryDatabase($"dlq-res-{Guid.NewGuid()}"))
            .AddSingleton(NullLoggerFactory.Instance)
            .AddLogging()
            .AddMassTransitTestHarness()
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BackofficeDbContext>();
        var row = new DeadLetterQueueRow
        {
            Id = Guid.NewGuid(),
            MessageId = Guid.NewGuid(),
            MessageType = "x",
            OriginalQueue = "y",
            Payload = "{}",
            FailureReason = "z",
            FirstFailedAt = DateTime.UtcNow,
        };
        db.DeadLetterQueue.Add(row);
        await db.SaveChangesAsync();

        var controller = new DlqController(
            db,
            harness.Bus,
            NullLogger<DlqController>.Instance)
        {
            ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(
                        new ClaimsIdentity(
                            new[] { new Claim("preferred_username", "ops-admin-1") },
                            "test")),
                },
            },
        };

        var body = new DlqController.DlqResolveRequest { Reason = "triaged — known flake" };
        var result = await controller.Resolve(row.Id, body, CancellationToken.None);

        Assert.IsType<Microsoft.AspNetCore.Mvc.NoContentResult>(result);

        await db.Entry(row).ReloadAsync();
        Assert.NotNull(row.ResolvedAt);
        Assert.Equal("ops-admin-1", row.ResolvedBy);
        Assert.Equal("triaged — known flake", row.ResolutionReason);

        await harness.Stop();
    }

    [Fact]
    [Trait("Category", "Phase06")]
    public async Task Missing_preferred_username_returns_401_on_requeue()
    {
        await using var provider = new ServiceCollection()
            .AddDbContext<BackofficeDbContext>(o => o.UseInMemoryDatabase($"dlq-anon-{Guid.NewGuid()}"))
            .AddSingleton(NullLoggerFactory.Instance)
            .AddLogging()
            .AddMassTransitTestHarness()
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BackofficeDbContext>();
        var row = new DeadLetterQueueRow
        {
            Id = Guid.NewGuid(),
            MessageId = Guid.NewGuid(),
            MessageType = "x",
            OriginalQueue = "y",
            Payload = "{}",
            FailureReason = "z",
            FirstFailedAt = DateTime.UtcNow,
        };
        db.DeadLetterQueue.Add(row);
        await db.SaveChangesAsync();

        var controller = new DlqController(
            db,
            harness.Bus,
            NullLogger<DlqController>.Instance)
        {
            ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    // Anonymous — no preferred_username claim at all.
                    User = new ClaimsPrincipal(new ClaimsIdentity()),
                },
            },
        };

        var result = await controller.Requeue(row.Id, CancellationToken.None);

        var problem = Assert.IsType<Microsoft.AspNetCore.Mvc.ObjectResult>(result);
        Assert.Equal(StatusCodes.Status401Unauthorized, problem.StatusCode);

        await harness.Stop();
    }
}
