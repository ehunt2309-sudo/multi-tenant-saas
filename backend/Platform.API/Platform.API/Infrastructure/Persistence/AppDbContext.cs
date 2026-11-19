using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Platform.API.Core.Entities;
using Platform.API.Core.Interfaces;
using Platform.API.Modules.Accounts;
using Platform.API.Modules.Bookings;
using Platform.API.Modules.Catalog;
using Platform.API.Modules.Cms;
using Platform.API.Modules.Customers;
using Platform.API.Modules.Loyalty;
using Platform.API.Modules.Promotions;
using Platform.API.Modules.Scheduling;
using Platform.API.Modules.Staffing;
using Platform.API.Modules.Tenants;

namespace Platform.API.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options, ITenantProvider tenantProvider, ICurrentUser currentUser)
    : DbContext(options)
{
    // Not tenant-scoped
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<TenantDomain> TenantDomains => Set<TenantDomain>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    // Tenant-scoped (global query filter on TenantId)
    public DbSet<User> Users => Set<User>();
    public DbSet<StaffMember> StaffMembers => Set<StaffMember>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<ServiceOffering> ServiceOfferings => Set<ServiceOffering>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<LoyaltyAccount> LoyaltyAccounts => Set<LoyaltyAccount>();
    public DbSet<LoyaltyTransaction> LoyaltyTransactions => Set<LoyaltyTransaction>();
    public DbSet<Promotion> Promotions => Set<Promotion>();
    public DbSet<Shift> Shifts => Set<Shift>();
    public DbSet<CmsPage> CmsPages => Set<CmsPage>();

    /// <summary>Guid.Empty when no tenant is resolved => the filter matches nothing (fails closed).</summary>
    private Guid CurrentTenantId => tenantProvider.TenantId ?? Guid.Empty;

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<decimal>().HavePrecision(18, 2);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        var tenantTypes = modelBuilder.Model.GetEntityTypes()
            .Where(t => typeof(TenantEntity).IsAssignableFrom(t.ClrType))
            .Select(t => t.ClrType).ToList();

        var method = typeof(AppDbContext).GetMethod(nameof(ConfigureTenantEntity), BindingFlags.NonPublic | BindingFlags.Instance)!;
        foreach (var type in tenantTypes)
            method.MakeGenericMethod(type).Invoke(this, [modelBuilder]);

        // No accidental cascade deletes anywhere: deletes are explicit (and most data is soft-deleted).
        foreach (var fk in modelBuilder.Model.GetEntityTypes().SelectMany(e => e.GetForeignKeys()))
            fk.DeleteBehavior = DeleteBehavior.Restrict;
    }

    private void ConfigureTenantEntity<T>(ModelBuilder modelBuilder) where T : TenantEntity
    {
        var entity = modelBuilder.Entity<T>();
        entity.HasQueryFilter(e => e.TenantId == CurrentTenantId);
        entity.HasIndex(e => e.TenantId);
        entity.HasOne<Tenant>().WithMany().HasForeignKey(e => e.TenantId);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        ApplyTenantAndAuditRules();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ApplyTenantAndAuditRules();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    /// <summary>
    /// Last line of defence for isolation: stamps TenantId on new rows and refuses any write that
    /// touches another tenant's data, even if a bug upstream tried to.
    /// </summary>
    private void ApplyTenantAndAuditRules()
    {
        var now = DateTime.UtcNow;
        var tenantId = tenantProvider.TenantId;
        var userId = currentUser.UserId;

        foreach (var entry in ChangeTracker.Entries<TenantEntity>())
        {
            var e = entry.Entity;

            if (entry.State == EntityState.Added && e.TenantId == Guid.Empty)
                e.TenantId = tenantId ?? throw new InvalidOperationException("Tenant-scoped data cannot be saved without a tenant context.");

            if (entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted
                && tenantId is not null && e.TenantId != tenantId)
                throw new InvalidOperationException("Cross-tenant write blocked.");

            if (entry.State == EntityState.Modified && entry.Property(x => x.TenantId).IsModified)
                throw new InvalidOperationException("TenantId is immutable.");

            if (e is AuditableEntity a)
            {
                if (entry.State == EntityState.Added)
                {
                    a.CreatedAt = now;
                    a.CreatedBy = userId;
                }
                else if (entry.State == EntityState.Modified)
                {
                    a.UpdatedAt = now;
                    a.UpdatedBy = userId;
                }
            }
        }
    }
}
