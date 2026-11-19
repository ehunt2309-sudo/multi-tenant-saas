using Platform.API.Infrastructure.Persistence;

namespace Platform.API.Modules.Accounts;

/// <summary>Stages an audit row; it is persisted by the caller's SaveChanges (same transaction).</summary>
public sealed class AuditService(AppDbContext db)
{
    public void Record(string action, Guid? userId, Guid? tenantId, string? ip, string? detail = null)
    {
        db.AuditLogs.Add(new AuditLog
        {
            Action = action,
            UserId = userId,
            TenantId = tenantId,
            IpAddress = ip,
            Detail = detail is { Length: > 500 } ? detail[..500] : detail,
            OccurredAt = DateTime.UtcNow
        });
    }
}
