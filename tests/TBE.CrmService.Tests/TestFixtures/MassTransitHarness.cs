using MassTransit;
using MassTransit.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TBE.CrmService.Infrastructure;

namespace TBE.CrmService.Tests.TestFixtures;

/// <summary>
/// Plan 06-04 MassTransit in-memory harness factory. Returns a started
/// <see cref="ITestHarness"/> whose container has CrmDbContext registered
/// as EF InMemory so the 6 Phase-6 consumers can be driven without a
/// live SQL Server container.
///
/// <para>
/// This harness deliberately does NOT configure the EF outbox — outbox
/// semantics are covered by the dedicated <c>CrmRebuildReplayTests</c>
/// RED placeholder which requires a live SQL container to exercise the
/// InboxState dedup path (D-51).
/// </para>
/// </summary>
public static class MassTransitHarness
{
    public static async Task<(ITestHarness Harness, ServiceProvider Provider)> StartAsync(
        Action<IBusRegistrationConfigurator>? configureConsumers = null)
    {
        var services = new ServiceCollection();
        services.AddDbContext<CrmDbContext>(o =>
            o.UseInMemoryDatabase($"crm-tests-{Guid.NewGuid()}"));

        services.AddMassTransitTestHarness(cfg =>
        {
            configureConsumers?.Invoke(cfg);
        });

        var provider = services.BuildServiceProvider(validateScopes: false);
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();
        return (harness, provider);
    }
}
