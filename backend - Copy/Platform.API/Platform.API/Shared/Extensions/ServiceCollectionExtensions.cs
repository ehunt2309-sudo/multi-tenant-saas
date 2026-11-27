using Microsoft.EntityFrameworkCore;
using Platform.API.Core.Interfaces;
using Platform.API.Infrastructure.Persistence;
using Platform.API.Infrastructure.Security;
using Platform.API.Infrastructure.Tenancy;
using Platform.API.Modules.Accounts;
using Platform.API.Modules.Analytics;
using Platform.API.Modules.Bookings;
using Platform.API.Modules.Catalog;
using Platform.API.Modules.Cms;
using Platform.API.Modules.Customers;
using Platform.API.Modules.Loyalty;
using Platform.API.Modules.Promotions;
using Platform.API.Modules.Scheduling;
using Platform.API.Modules.Staffing;
using Platform.API.Modules.Tenants;

namespace Platform.API.Shared.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPlatformPersistence(this IServiceCollection services, IConfiguration config)
    {
        var connectionString = config.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException(
                "ConnectionStrings:DefaultConnection is missing. Set it in user-secrets (local) or Key Vault (ConnectionStrings--DefaultConnection).");

        services.AddDbContext<AppDbContext>(o => o.UseSqlServer(connectionString, sql =>
        {
            // Azure SQL drops connections during failover/scale events: retry transient failures automatically.
            sql.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(10), errorNumbersToAdd: null);
            sql.CommandTimeout(30);
        }));

        // Size-limited so hostile Host/Origin values can't grow the cache without bound.
        services.AddMemoryCache(o => o.SizeLimit = 10_000);
        services.AddHttpContextAccessor();
        services.AddScoped<ITenantProvider, TenantContext>();
        services.AddScoped<ICurrentUser, CurrentUser>();
        services.AddScoped<ITenantLookup, TenantLookup>();
        return services;
    }

    public static IServiceCollection AddPlatformModules(this IServiceCollection services)
    {
        services.AddSingleton<PasswordService>();
        services.AddSingleton<JwtTokenService>();

        services.AddScoped<AuditService>();
        services.AddScoped<AuthService>();
        services.AddScoped<TenantService>();
        services.AddScoped<CustomerService>();
        services.AddScoped<LoyaltyService>();
        services.AddScoped<ServiceOfferingService>();
        services.AddScoped<ProductService>();
        services.AddScoped<BookingService>();
        services.AddScoped<PromotionService>();
        services.AddScoped<StaffService>();
        services.AddScoped<ShiftService>();
        services.AddScoped<PageService>();
        services.AddScoped<AnalyticsService>();
        return services;
    }
}
