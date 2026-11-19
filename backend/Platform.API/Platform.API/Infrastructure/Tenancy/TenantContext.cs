using Platform.API.Core.Interfaces;

namespace Platform.API.Infrastructure.Tenancy;

/// <summary>Per-request holder of the resolved tenant. Scoped: never shared between requests.</summary>
public sealed class TenantContext : ITenantProvider
{
    public Guid? TenantId { get; private set; }
    public void SetTenant(Guid tenantId) => TenantId = tenantId;
}
