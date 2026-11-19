using Microsoft.EntityFrameworkCore;
using Platform.API.Infrastructure.Security;
using Platform.API.Modules.Accounts;
using Platform.API.Modules.Tenants;
using Platform.API.Shared.Constants;
using Platform.API.Shared.Utilities;

namespace Platform.API.Infrastructure.Persistence;

public static class DatabaseInitializer
{
    /// <summary>
    /// Optional migrate-on-start (Development / first deploy of a single instance) and an idempotent
    /// bootstrap of the first PlatformAdmin from config (Bootstrap:AdminEmail / Bootstrap:AdminPassword).
    /// Remove the bootstrap secrets from Key Vault once the admin exists.
    /// </summary>
    public static async Task InitializeAsync(IServiceProvider services, IConfiguration config)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        var log = sp.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseInitializer");

        if (config.GetValue<bool>("Database:MigrateOnStartup"))
        {
            log.LogInformation("Applying database migrations...");
            await db.Database.MigrateAsync();
        }

        var email = config["Bootstrap:AdminEmail"];
        var password = config["Bootstrap:AdminPassword"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password)) return;

        var passwords = sp.GetRequiredService<PasswordService>();
        passwords.EnforcePolicy(password, email);
        var normalized = TextHelper.NormalizeEmail(email);

        if (!await db.Tenants.AnyAsync(t => t.Id == PlatformConstants.PlatformTenantId))
            db.Tenants.Add(new Tenant { Id = PlatformConstants.PlatformTenantId, Name = "Platform", Slug = "platform", Industry = Industry.Other });

        var exists = await db.Users.IgnoreQueryFilters()
            .AnyAsync(u => u.TenantId == PlatformConstants.PlatformTenantId && u.NormalizedEmail == normalized);

        if (!exists)
        {
            var admin = new User
            {
                TenantId = PlatformConstants.PlatformTenantId,
                Email = email.Trim(),
                NormalizedEmail = normalized,
                FirstName = "Platform",
                LastName = "Admin",
                Role = Roles.PlatformAdmin
            };
            admin.PasswordHash = passwords.Hash(admin, password);
            db.Users.Add(admin);
            log.LogWarning("Bootstrapped platform admin {Email}. Remove Bootstrap:* secrets now.", email);
        }

        await db.SaveChangesAsync();
    }
}
