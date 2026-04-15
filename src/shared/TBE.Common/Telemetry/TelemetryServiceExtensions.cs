using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace TBE.Common.Telemetry;

/// <summary>
/// One-line OpenTelemetry wire-up for every TBE service. Registers
/// <see cref="SensitiveAttributeProcessor"/> BEFORE any exporter so no PCI / PII tag is ever
/// shipped to the OTLP collector (COMP-06 / T-03-05).
/// </summary>
public static class TelemetryServiceExtensions
{
    public static IServiceCollection AddTbeOpenTelemetry(
        this IServiceCollection services,
        IConfiguration configuration,
        string serviceName)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);

        services.AddSingleton<SensitiveAttributeProcessor>();

        var endpoint = new Uri(configuration["OpenTelemetry:OtlpEndpoint"] ?? "http://localhost:4317");

        services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService(serviceName))
            .WithTracing(t => t
                .AddSource("TBE.*")
                .AddSource("MassTransit")
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                // Scrub BEFORE the exporter — order is load-bearing.
                .AddProcessor<SensitiveAttributeProcessor>()
                .AddOtlpExporter(o => o.Endpoint = endpoint))
            .WithMetrics(m => m
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddOtlpExporter(o => o.Endpoint = endpoint));

        return services;
    }
}
