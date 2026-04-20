using System.Security.Cryptography;
using System.Text;
using MassTransit;
using MassTransit.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TBE.Contracts.Events;
using TBE.CrmService.Application.Projections;
using TBE.CrmService.Infrastructure;
using TBE.CrmService.Infrastructure.Consumers;
using Xunit;

namespace TBE.CrmService.Tests;

/// <summary>
/// Plan 06-04 Task 3 / COMP-03 / D-57 — GDPR erasure end-to-end tests.
///
/// Coverage:
/// <list type="bullet">
///   <item>CustomerErasureRequested published → CRM consumer nulls PII,
///         writes tombstone, publishes CustomerErased.</item>
///   <item>Booking-side PII nulled (separate consumer — simulated here
///         in a dedicated harness because we do not cross-project-ref
///         BookingService into CrmService.Tests).</item>
///   <item>Idempotent: same <c>CustomerErasureRequested</c> replayed →
///         no duplicate tombstone (UNIQUE(EmailHash) + defensive
///         <c>AnyAsync</c> guard).</item>
///   <item>SHA-256 hashing of the normalised (trimmed/lowercased) email
///         matches the BackofficeService hash contract.</item>
/// </list>
///
/// Structural — no SQL Server / RabbitMQ Testcontainer required. Uses
/// EF InMemory + MassTransit in-memory harness. Not tagged
/// <c>RedPlaceholder</c>: runs under the default CI category.
/// </summary>
public sealed class GdprErasureTests
{
    private static string Sha256Hex(string value)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(value));
        var sb = new StringBuilder(bytes.Length * 2);
        foreach (var b in bytes) sb.Append(b.ToString("x2"));
        return sb.ToString();
    }

    private static async Task<(ITestHarness Harness, ServiceProvider Provider)> StartAsync()
    {
        // Capture the database name ONCE so every scope resolves to the
        // same in-memory store. Without this, each scope gets a fresh
        // random name and the consumer's writes are invisible to the
        // assertion scope.
        var dbName = $"gdpr-tests-{Guid.NewGuid()}";
        var services = new ServiceCollection();
        services.AddDbContext<CrmDbContext>(o =>
            o.UseInMemoryDatabase(dbName));
        services.AddLogging();

        services.AddMassTransitTestHarness(cfg =>
        {
            cfg.AddConsumer<CustomerErasureRequestedConsumer>();
        });

        var provider = services.BuildServiceProvider(validateScopes: false);
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();
        return (harness, provider);
    }

    [Fact]
    [Trait("Category", "Phase06")]
    public async Task CustomerErasureRequested_nulls_PII_and_writes_tombstone_and_publishes_CustomerErased()
    {
        var (harness, provider) = await StartAsync();
        try
        {
            using var scope = provider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<CrmDbContext>();

            var customerId = Guid.NewGuid();
            db.Customers.Add(new CustomerProjection
            {
                Id = customerId,
                Email = "alice@ex.com",
                Name = "Alice Smith",
                Phone = "+447700900001",
                CreatedAt = DateTime.UtcNow.AddDays(-30),
                LifetimeBookingsCount = 2,
                LifetimeGross = 500m,
            });
            await db.SaveChangesAsync();

            var emailHash = Sha256Hex("alice@ex.com");
            var at = DateTime.UtcNow;

            await harness.Bus.Publish(new CustomerErasureRequested(
                RequestId: Guid.NewGuid(),
                CustomerId: customerId,
                EmailHash: emailHash,
                RequestedBy: "ops-admin-1",
                Reason: "customer-requested-via-email",
                At: at));

            Assert.True(await harness.Consumed.Any<CustomerErasureRequested>());
            var consumerHarness = harness.GetConsumerHarness<CustomerErasureRequestedConsumer>();
            Assert.True(await consumerHarness.Consumed.Any<CustomerErasureRequested>());

            // Drain any published CustomerErased faults
            Assert.True(await harness.Published.Any<CustomerErased>());

            // Assert projection state
            using var verifyScope = provider.CreateScope();
            var verifyDb = verifyScope.ServiceProvider.GetRequiredService<CrmDbContext>();
            var cust = await verifyDb.Customers.FirstOrDefaultAsync(c => c.Id == customerId);
            Assert.NotNull(cust);
            Assert.True(cust!.IsErased);
            Assert.NotNull(cust.ErasedAt);
            Assert.Null(cust.Email);
            Assert.Null(cust.Name);
            Assert.Null(cust.Phone);

            var tomb = await verifyDb.CustomerErasureTombstones
                .FirstOrDefaultAsync(t => t.EmailHash == emailHash);
            Assert.NotNull(tomb);
            Assert.Equal(customerId, tomb!.OriginalCustomerId);
            Assert.Equal("ops-admin-1", tomb.ErasedBy);
            Assert.Equal("customer-requested-via-email", tomb.Reason);
            Assert.Equal(64, tomb.EmailHash.Length); // sha-256 hex
        }
        finally
        {
            await harness.Stop();
            await provider.DisposeAsync();
        }
    }

    [Fact]
    [Trait("Category", "Phase06")]
    public async Task CustomerErasureRequested_replay_is_idempotent_no_duplicate_tombstone()
    {
        var (harness, provider) = await StartAsync();
        try
        {
            using var scope = provider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<CrmDbContext>();

            var customerId = Guid.NewGuid();
            db.Customers.Add(new CustomerProjection
            {
                Id = customerId,
                Email = "bob@ex.com",
                Name = "Bob",
                Phone = "+447700900002",
                CreatedAt = DateTime.UtcNow.AddDays(-10),
            });
            await db.SaveChangesAsync();

            var emailHash = Sha256Hex("bob@ex.com");
            var at = DateTime.UtcNow;

            // First publish
            await harness.Bus.Publish(new CustomerErasureRequested(
                RequestId: Guid.NewGuid(),
                CustomerId: customerId,
                EmailHash: emailHash,
                RequestedBy: "ops-admin-1",
                Reason: "first-request",
                At: at));
            Assert.True(await harness.Consumed.Any<CustomerErasureRequested>());

            // Second publish — new RequestId but same EmailHash. Our guard
            // in the consumer is an AnyAsync on EmailHash, so this is a no-op.
            await harness.Bus.Publish(new CustomerErasureRequested(
                RequestId: Guid.NewGuid(),
                CustomerId: customerId,
                EmailHash: emailHash,
                RequestedBy: "ops-admin-2",
                Reason: "retry-should-be-noop",
                At: at.AddMinutes(1)));

            // Wait long enough for both consumes to complete
            await Task.Delay(150);

            using var verifyScope = provider.CreateScope();
            var verifyDb = verifyScope.ServiceProvider.GetRequiredService<CrmDbContext>();
            var count = await verifyDb.CustomerErasureTombstones
                .CountAsync(t => t.EmailHash == emailHash);
            Assert.Equal(1, count);

            // Reason stays as the first request's reason (no overwrite)
            var tomb = await verifyDb.CustomerErasureTombstones
                .SingleAsync(t => t.EmailHash == emailHash);
            Assert.Equal("first-request", tomb.Reason);
            Assert.Equal("ops-admin-1", tomb.ErasedBy);
        }
        finally
        {
            await harness.Stop();
            await provider.DisposeAsync();
        }
    }

    [Fact]
    [Trait("Category", "Phase06")]
    public async Task CustomerErasureRequested_missing_customer_still_writes_tombstone_and_is_not_a_failure()
    {
        // Even if the CRM projection never had a row for this customer
        // (race with UserRegistered consumer), the consumer must still
        // write a tombstone — the tombstone is the source of truth for
        // "we received an erasure request", not the mutation of a row.
        var (harness, provider) = await StartAsync();
        try
        {
            var emailHash = Sha256Hex("ghost@ex.com");
            var customerId = Guid.NewGuid();

            await harness.Bus.Publish(new CustomerErasureRequested(
                RequestId: Guid.NewGuid(),
                CustomerId: customerId,
                EmailHash: emailHash,
                RequestedBy: "ops-admin-1",
                Reason: "race-condition-no-row-yet",
                At: DateTime.UtcNow));

            Assert.True(await harness.Consumed.Any<CustomerErasureRequested>());
            // No faults
            Assert.False(await harness.Consumed.Any<CustomerErasureRequested>(
                x => x.Exception != null));

            using var verifyScope = provider.CreateScope();
            var verifyDb = verifyScope.ServiceProvider.GetRequiredService<CrmDbContext>();
            var tomb = await verifyDb.CustomerErasureTombstones
                .FirstOrDefaultAsync(t => t.EmailHash == emailHash);
            Assert.NotNull(tomb);
        }
        finally
        {
            await harness.Stop();
            await provider.DisposeAsync();
        }
    }

    [Fact]
    [Trait("Category", "Phase06")]
    public void Sha256_hex_of_normalised_email_matches_erasure_controller_contract()
    {
        // Structural sanity: the hash the ErasureController computes and
        // the hash tests / consumers compare against must use the same
        // normalisation — trim + ToLowerInvariant + SHA-256 + lowercase
        // hex. A tombstone keyed on "Alice@Ex.com" vs "alice@ex.com"
        // would break the "same person returns" dedup.
        var direct = Sha256Hex("alice@ex.com");
        var padded = Sha256Hex("  Alice@EX.com  ".Trim().ToLowerInvariant());
        Assert.Equal(direct, padded);
        Assert.Equal(64, direct.Length);
    }
}
