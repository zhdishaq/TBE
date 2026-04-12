using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Serilog.Formatting.Compact;

// Bootstrap logger — before builder is created
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(new CompactJsonFormatter())
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, configuration) =>
        configuration.ReadFrom.Configuration(context.Configuration)
                     .ReadFrom.Services(services)
                     .Enrich.FromLogContext()
                     .Enrich.WithProperty("Service", "TBE.Gateway"));

    var keycloakBaseUrl = builder.Configuration["Keycloak:BaseUrl"]
        ?? "http://keycloak:8080";

    // Three JWT bearer schemes — one per Keycloak realm
    // CRITICAL: each scheme must be named and each policy must call AddAuthenticationSchemes()
    // Without AddAuthenticationSchemes(), ASP.NET Core tries all schemes and returns inconsistent 401s
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer("B2C", options =>
        {
            options.Authority = $"{keycloakBaseUrl}/realms/tbe-b2c";
            options.RequireHttpsMetadata = false; // dev only — set true in production
            options.Audience = "tbe-gateway";
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = $"{keycloakBaseUrl}/realms/tbe-b2c",
                ValidateAudience = false, // Keycloak puts audience in 'azp' not 'aud' by default
                ValidateLifetime = true
            };
        })
        .AddJwtBearer("B2B", options =>
        {
            options.Authority = $"{keycloakBaseUrl}/realms/tbe-b2b";
            options.RequireHttpsMetadata = false;
            options.Audience = "tbe-gateway";
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = $"{keycloakBaseUrl}/realms/tbe-b2b",
                ValidateAudience = false,
                ValidateLifetime = true
            };
        })
        .AddJwtBearer("Backoffice", options =>
        {
            options.Authority = $"{keycloakBaseUrl}/realms/tbe-backoffice";
            options.RequireHttpsMetadata = false;
            options.Audience = "tbe-gateway";
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = $"{keycloakBaseUrl}/realms/tbe-backoffice",
                ValidateAudience = false,
                ValidateLifetime = true
            };
        });

    builder.Services.AddAuthorization(options =>
    {
        options.AddPolicy("B2CPolicy", policy =>
            policy.AddAuthenticationSchemes("B2C")
                  .RequireAuthenticatedUser());
        options.AddPolicy("B2BPolicy", policy =>
            policy.AddAuthenticationSchemes("B2B")
                  .RequireAuthenticatedUser());
        options.AddPolicy("BackofficePolicy", policy =>
            policy.AddAuthenticationSchemes("Backoffice")
                  .RequireAuthenticatedUser());
    });

    builder.Services.AddReverseProxy()
        .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

    builder.Services.AddHealthChecks();

    var app = builder.Build();

    app.UseSerilogRequestLogging();
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapReverseProxy();
    app.MapHealthChecks("/health");

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "TBE.Gateway terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
