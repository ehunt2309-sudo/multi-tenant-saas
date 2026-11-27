using System.Text.Json.Serialization;
using Azure.Identity;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Platform.API.Api.Configuration;
using Platform.API.Api.Middleware;
using Platform.API.Infrastructure.Persistence;
using Platform.API.Shared.Extensions;
using Platform.API.Shared.Utilities;
using Serilog;

Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Secrets (connection string, JWT key, bootstrap admin) come from Azure Key Vault via Managed Identity.
    // Key names use "--" for nesting, e.g. ConnectionStrings--DefaultConnection, Jwt--SigningKey.
    var keyVaultUri = builder.Configuration["KeyVault:Uri"];
    if (!string.IsNullOrWhiteSpace(keyVaultUri))
        builder.Configuration.AddAzureKeyVault(new Uri(keyVaultUri), new DefaultAzureCredential());

    builder.Host.UseSerilog((ctx, _, cfg) => cfg
        .ReadFrom.Configuration(ctx.Configuration)
        .Enrich.FromLogContext()
        .WriteTo.Console());

    builder.WebHost.ConfigureKestrel(o =>
    {
        o.AddServerHeader = false;                 // don't advertise the stack
        o.Limits.MaxRequestBodySize = 1_048_576;   // 1 MB cap on request bodies
    });

    // Behind Azure App Service / Front Door the real client IP arrives in X-Forwarded-For (needed for rate limiting).
    builder.Services.Configure<ForwardedHeadersOptions>(o =>
    {
        o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        o.ForwardLimit = 1;
        o.KnownNetworks.Clear();
        o.KnownProxies.Clear();
    });

    builder.Services
        .AddPlatformPersistence(builder.Configuration)
        .AddPlatformAuthentication()
        .AddPlatformCors()
        .AddPlatformRateLimiting()
        .AddPlatformModules();

    builder.Services.AddControllers(o => o.Filters.Add<TenantRequiredFilter>())
        .AddJsonOptions(o =>
        {
            o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            o.JsonSerializerOptions.Converters.Add(new UtcDateTimeConverter());
            o.JsonSerializerOptions.Converters.Add(new UtcNullableDateTimeConverter());
        });

    builder.Services.AddProblemDetails();
    builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
    builder.Services.AddOpenApi();

    builder.Services.AddHealthChecks()
        .AddCheck<DatabaseHealthCheck>("database", tags: ["ready"]);

    // Application Insights switches on automatically when APPLICATIONINSIGHTS_CONNECTION_STRING is set.
    if (!string.IsNullOrWhiteSpace(builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]))
        builder.Services.AddApplicationInsightsTelemetry();

    var app = builder.Build();

    app.UseForwardedHeaders();
    app.UseExceptionHandler();
    app.UseStatusCodePages();
    if (!app.Environment.IsDevelopment()) app.UseHsts();
    app.UseHttpsRedirection();
    app.UseMiddleware<SecurityHeadersMiddleware>();
    app.UseSerilogRequestLogging();
    app.UseCors();
    app.UseRateLimiter();
    app.UseAuthentication();
    app.UseMiddleware<TenantResolutionMiddleware>();   // after auth: the JWT decides the tenant for logged-in users
    app.UseAuthorization();

    if (app.Environment.IsDevelopment())
        app.MapOpenApi().AllowAnonymous();

    app.MapControllers();
    app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false }).AllowAnonymous();
    app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = r => r.Tags.Contains("ready") }).AllowAnonymous();

    await DatabaseInitializer.InitializeAsync(app.Services, app.Configuration);

    await app.RunAsync();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}
