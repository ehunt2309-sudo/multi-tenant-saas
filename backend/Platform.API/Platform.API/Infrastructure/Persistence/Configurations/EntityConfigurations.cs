using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
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

namespace Platform.API.Infrastructure.Persistence.Configurations;

public sealed class TenantConfig : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> b)
    {
        b.Property(x => x.Name).HasMaxLength(150).IsRequired();
        b.Property(x => x.Slug).HasMaxLength(60).IsRequired();
        b.Property(x => x.Industry).HasConversion<string>().HasMaxLength(32);
        b.Property(x => x.PrimaryColor).HasMaxLength(7);
        b.Property(x => x.LogoUrl).HasMaxLength(500);
        b.Property(x => x.Currency).HasMaxLength(3);
        b.Property(x => x.TimeZone).HasMaxLength(64);
        b.Property(x => x.LoyaltyPointsPerCurrencyUnit).HasPrecision(9, 4);
        b.HasIndex(x => x.Slug).IsUnique();
    }
}

public sealed class TenantDomainConfig : IEntityTypeConfiguration<TenantDomain>
{
    public void Configure(EntityTypeBuilder<TenantDomain> b)
    {
        b.Property(x => x.Host).HasMaxLength(253).IsRequired();
        b.HasIndex(x => x.Host).IsUnique();   // a host belongs to exactly one tenant
        b.HasOne(x => x.Tenant).WithMany(t => t.Domains).HasForeignKey(x => x.TenantId);
    }
}

public sealed class UserConfig : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> b)
    {
        b.Property(x => x.Email).HasMaxLength(256).IsRequired();
        b.Property(x => x.NormalizedEmail).HasMaxLength(256).IsRequired();
        b.Property(x => x.PasswordHash).HasMaxLength(512).IsRequired();
        b.Property(x => x.FirstName).HasMaxLength(100);
        b.Property(x => x.LastName).HasMaxLength(100);
        b.Property(x => x.Role).HasMaxLength(32).IsRequired();
        b.HasIndex(x => new { x.TenantId, x.NormalizedEmail }).IsUnique();
    }
}

public sealed class RefreshTokenConfig : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> b)
    {
        b.Property(x => x.TokenHash).HasMaxLength(64).IsRequired();
        b.Property(x => x.CreatedByIp).HasMaxLength(64);
        b.Property(x => x.ReplacedByTokenHash).HasMaxLength(64);
        b.HasIndex(x => x.TokenHash).IsUnique();
        b.HasIndex(x => x.FamilyId);
        b.HasIndex(x => x.UserId);
    }
}

public sealed class AuditLogConfig : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> b)
    {
        b.Property(x => x.Action).HasMaxLength(64).IsRequired();
        b.Property(x => x.IpAddress).HasMaxLength(64);
        b.Property(x => x.Detail).HasMaxLength(500);
        b.HasIndex(x => new { x.TenantId, x.OccurredAt });
    }
}

public sealed class StaffMemberConfig : IEntityTypeConfiguration<StaffMember>
{
    public void Configure(EntityTypeBuilder<StaffMember> b)
    {
        b.Property(x => x.JobTitle).HasMaxLength(100);
        b.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId);
        b.HasIndex(x => x.UserId).IsUnique();
    }
}

public sealed class CustomerConfig : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> b)
    {
        b.Property(x => x.FirstName).HasMaxLength(100);
        b.Property(x => x.LastName).HasMaxLength(100);
        b.Property(x => x.Email).HasMaxLength(256);
        b.Property(x => x.Phone).HasMaxLength(32);
        b.Property(x => x.Gender).HasMaxLength(32);
        b.HasIndex(x => x.UserId).IsUnique().HasFilter("[UserId] IS NOT NULL");
        b.HasIndex(x => new { x.TenantId, x.LastName, x.FirstName });
        b.HasIndex(x => new { x.TenantId, x.TotalSpend });
    }
}

public sealed class ServiceOfferingConfig : IEntityTypeConfiguration<ServiceOffering>
{
    public void Configure(EntityTypeBuilder<ServiceOffering> b)
    {
        b.Property(x => x.Name).HasMaxLength(150).IsRequired();
        b.Property(x => x.Description).HasMaxLength(2000);
        b.HasIndex(x => new { x.TenantId, x.IsActive });
    }
}

public sealed class ProductConfig : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> b)
    {
        b.Property(x => x.Name).HasMaxLength(150).IsRequired();
        b.Property(x => x.Description).HasMaxLength(2000);
        b.Property(x => x.ImageUrl).HasMaxLength(500);
        b.HasIndex(x => new { x.TenantId, x.IsActive });
    }
}

public sealed class BookingConfig : IEntityTypeConfiguration<Booking>
{
    public void Configure(EntityTypeBuilder<Booking> b)
    {
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
        b.Property(x => x.Notes).HasMaxLength(500);
        b.Property(x => x.RowVersion).IsRowVersion();
        b.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId);
        b.HasOne(x => x.StaffMember).WithMany().HasForeignKey(x => x.StaffMemberId);
        b.HasOne(x => x.ServiceOffering).WithMany().HasForeignKey(x => x.ServiceOfferingId);
        b.HasIndex(x => new { x.TenantId, x.StartsAt });
        b.HasIndex(x => new { x.TenantId, x.StaffMemberId, x.StartsAt });
        b.HasIndex(x => new { x.TenantId, x.CustomerId, x.StartsAt });
    }
}

public sealed class LoyaltyAccountConfig : IEntityTypeConfiguration<LoyaltyAccount>
{
    public void Configure(EntityTypeBuilder<LoyaltyAccount> b)
    {
        b.Property(x => x.Tier).HasConversion<string>().HasMaxLength(32);
        b.Property(x => x.RowVersion).IsRowVersion();
        b.HasOne<Customer>().WithMany().HasForeignKey(x => x.CustomerId);
        b.HasIndex(x => x.CustomerId).IsUnique();
    }
}

public sealed class LoyaltyTransactionConfig : IEntityTypeConfiguration<LoyaltyTransaction>
{
    public void Configure(EntityTypeBuilder<LoyaltyTransaction> b)
    {
        b.Property(x => x.Type).HasConversion<string>().HasMaxLength(32);
        b.Property(x => x.Reason).HasMaxLength(200);
        b.HasOne<LoyaltyAccount>().WithMany().HasForeignKey(x => x.LoyaltyAccountId);
        b.HasIndex(x => new { x.LoyaltyAccountId, x.CreatedAt });
        // A booking can only ever earn points once.
        b.HasIndex(x => x.BookingId).IsUnique().HasFilter("[BookingId] IS NOT NULL");
    }
}

public sealed class PromotionConfig : IEntityTypeConfiguration<Promotion>
{
    public void Configure(EntityTypeBuilder<Promotion> b)
    {
        b.Property(x => x.Title).HasMaxLength(150).IsRequired();
        b.Property(x => x.Description).HasMaxLength(2000);
        b.Property(x => x.DiscountType).HasConversion<string>().HasMaxLength(32);
        b.HasIndex(x => new { x.TenantId, x.IsActive, x.StartsAt, x.EndsAt });
    }
}

public sealed class ShiftConfig : IEntityTypeConfiguration<Shift>
{
    public void Configure(EntityTypeBuilder<Shift> b)
    {
        b.Property(x => x.Notes).HasMaxLength(500);
        b.HasOne(x => x.StaffMember).WithMany().HasForeignKey(x => x.StaffMemberId);
        b.HasIndex(x => new { x.TenantId, x.StaffMemberId, x.StartsAt });
    }
}

public sealed class CmsPageConfig : IEntityTypeConfiguration<CmsPage>
{
    public void Configure(EntityTypeBuilder<CmsPage> b)
    {
        b.Property(x => x.Slug).HasMaxLength(100).IsRequired();
        b.Property(x => x.Title).HasMaxLength(150).IsRequired();
        b.Property(x => x.ContentJson).IsRequired();
        b.HasIndex(x => new { x.TenantId, x.Slug }).IsUnique();
    }
}
