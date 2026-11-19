namespace Platform.API.Core.Entities;

/// <summary>
/// Data owned by exactly one client business. AppDbContext applies a global query filter on
/// TenantId (fails closed when no tenant is resolved) and blocks cross-tenant writes.
/// The Tenant entity itself does NOT inherit this - it is the owner, not owned.
/// </summary>
public abstract class TenantEntity : BaseEntity
{
    public Guid TenantId { get; set; }
}
