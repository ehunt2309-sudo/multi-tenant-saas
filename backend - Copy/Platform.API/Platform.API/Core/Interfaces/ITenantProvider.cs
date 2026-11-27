namespace Platform.API.Core.Interfaces;

/// <summary>Which client business is the current request for? Null = none resolved.</summary>
public interface ITenantProvider
{
    Guid? TenantId { get; }
    void SetTenant(Guid tenantId);
}
