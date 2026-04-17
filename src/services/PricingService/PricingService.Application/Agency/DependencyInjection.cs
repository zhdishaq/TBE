using MassTransit;
using Microsoft.Extensions.DependencyInjection;

namespace TBE.PricingService.Application.Agency;

/// <summary>
/// Plan 05-02 Task 1 — registers B2B agency-markup components on the host's
/// MassTransit configurator. Call this from <c>Program.cs</c> inside the
/// <c>AddMassTransit(...)</c> lambda.
/// </summary>
/// <remarks>
/// The concrete <see cref="IAgencyMarkupRulesEngine"/> implementation lives in
/// the Infrastructure project (references <c>PricingDbContext</c>); the API
/// host registers it as a scoped service alongside this call.
/// </remarks>
public static class DependencyInjection
{
    /// <summary>
    /// Adds <see cref="AgencyPriceRequestedConsumer"/> to a MassTransit
    /// configurator. Use from <c>builder.Services.AddMassTransit(cfg =&gt; { cfg.AddAgencyPricingConsumers(); ... })</c>.
    /// </summary>
    public static IBusRegistrationConfigurator AddAgencyPricingConsumers(
        this IBusRegistrationConfigurator cfg)
    {
        cfg.AddConsumer<AgencyPriceRequestedConsumer>();
        return cfg;
    }
}
