namespace Platform.API.Core.Entities;

/// <summary>Tenant-owned entity with automatic created/updated tracking (set in SaveChanges, UTC).</summary>
public abstract class AuditableEntity : TenantEntity
{
    public DateTime CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
}
