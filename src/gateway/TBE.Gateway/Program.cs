using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Serilog.Formatting.Compact;
using TBE.Common.Telemetry;

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
        // Plan 05-01 Task 3: ValidateAudience=true flipped from staged=false.
        // verify-audience-smoke-b2b.sh MUST pass in the target env before deploy.
        // Rollback: set ValidateAudience=false and redeploy.
        //
        // The scheme is named "tbe-b2b" (not the legacy short "B2B") so the
        // audience-confusion mitigation (Pitfall 4 / T-05-01-01) is
        // grep-verifiable against the plan's acceptance criteria. The
        // "B2BPolicy" policy name below is preserved so appsettings.json
        // ReverseProxy.Routes do not need a reauth re-plumb.
        .AddJwtBearer("tbe-b2b", options =>
        {
            options.Authority = builder.Configuration["Keycloak:B2B:Issuer"]
                ?? $"{keycloakBaseUrl}/realms/tbe-b2b";
            options.RequireHttpsMetadata = builder.Environment.IsProduction();
            options.Audience = "tbe-api";
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = builder.Configuration["Keycloak:B2B:Issuer"]
                    ?? $"{keycloakBaseUrl}/realms/tbe-b2b",
                ValidateAudience = true, // T-05-01-01 — flipped from false
                ValidAudience = "tbe-api",
                ValidateIssuerSigningKey = true,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(30),
            };
            options.Events = new JwtBearerEvents
            {
                // Keycloak emits realm roles under a JSON "realm_access"
                // envelope instead of top-level claims. Expand them into
                // flat "roles" claims so B2BPolicy / B2BAdminPolicy can
                // assert via HasClaim("roles", …) without every downstream
                // having to parse the envelope itself.
                OnTokenValidated = ctx =>
                {
                    var realmAccess = ctx.Principal?.FindFirst("realm_access")?.Value;
                    if (!string.IsNullOrEmpty(realmAccess))
                    {
                        using var doc = JsonDocument.Parse(realmAccess);
                        if (doc.RootElement.TryGetProperty("roles", out var rolesEl))
                        {
                            var identity = (ClaimsIdentity)ctx.Principal!.Identity!;
                            foreach (var role in rolesEl.EnumerateArray())
                            {
                                identity.AddClaim(new Claim("roles", role.GetString() ?? string.Empty));
                            }
                        }
                    }
                    return Task.CompletedTask;
                },
            };
        })
        // Plan 06-01 Task 3 — audience flipped to "tbe-api" + ValidateAudience=true
        // (Pitfall 4 pin). The tbe-backoffice realm's tbe-backoffice-ui OIDC
        // client carries an audience mapper emitting "tbe-api" so every
        // BackofficeService route refuses a B2B or B2C token at the gateway
        // edge. Realm_access.roles are projected into flat "roles" claims so
        // BackofficePolicy (and downstream BackofficeService policies) can
        // assert via HasClaim("roles", …).
        .AddJwtBearer("Backoffice", options =>
        {
            options.Authority = builder.Configuration["Keycloak:Backoffice:Issuer"]
                ?? $"{keycloakBaseUrl}/realms/tbe-backoffice";
            options.RequireHttpsMetadata = builder.Environment.IsProduction();
            options.Audience = "tbe-api";
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = builder.Configuration["Keycloak:Backoffice:Issuer"]
                    ?? $"{keycloakBaseUrl}/realms/tbe-backoffice",
                ValidateAudience = true,
                ValidAudience = "tbe-api",
                ValidateIssuerSigningKey = true,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(30),
            };
            options.Events = new JwtBearerEvents
            {
                OnTokenValidated = ctx =>
                {
                    var realmAccess = ctx.Principal?.FindFirst("realm_access")?.Value;
                    if (!string.IsNullOrEmpty(realmAccess))
                    {
                        using var doc = JsonDocument.Parse(realmAccess);
                        if (doc.RootElement.TryGetProperty("roles", out var rolesEl))
                        {
                            var identity = (ClaimsIdentity)ctx.Principal!.Identity!;
                            foreach (var role in rolesEl.EnumerateArray())
                            {
                                identity.AddClaim(new Claim("roles", role.GetString() ?? string.Empty));
                            }
                        }
                    }
                    return Task.CompletedTask;
                },
            };
        });

    builder.Services.AddAuthorization(options =>
    {
        options.AddPolicy("B2CPolicy", policy =>
            policy.AddAuthenticationSchemes("B2C")
                  .RequireAuthenticatedUser());
        // B2BPolicy — any agent role (D-32). The scheme pin
        // (AddAuthenticationSchemes("tbe-b2b")) closes the Pitfall 4
        // loop: a B2C token never satisfies this policy even if
        // the caller targets /api/b2b/*.
        options.AddPolicy("B2BPolicy", policy =>
            policy.AddAuthenticationSchemes("tbe-b2b")
                  .RequireAuthenticatedUser()
                  .RequireAssertion(ctx =>
                      ctx.User.HasClaim("roles", "agent") ||
                      ctx.User.HasClaim("roles", "agent-admin") ||
                      ctx.User.HasClaim("roles", "agent-readonly")));
        // B2BAdminPolicy — agent-admin only. Used on admin-only
        // routes (sub-agent CRUD at the gateway edge, wallet top-up,
        // per-booking markup override). T-05-01-02 mitigation.
        options.AddPolicy("B2BAdminPolicy", policy =>
            policy.AddAuthenticationSchemes("tbe-b2b")
                  .RequireAuthenticatedUser()
                  .RequireClaim("roles", "agent-admin"));
        options.AddPolicy("BackofficePolicy", policy =>
            policy.AddAuthenticationSchemes("Backoffice")
                  .RequireAuthenticatedUser());
    });

    builder.Services.AddReverseProxy()
        .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

    builder.Services.AddHealthChecks();
    builder.Services.AddTbeSwagger("Gateway");

    var app = builder.Build();

    app.UseSerilogRequestLogging();
    app.UseAuthentication();
    app.UseAuthorization();

    if (app.Environment.IsDevelopment())
    {
        app.UseTbeSwagger();
    }

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
