using Platform.API.Core.Entities;

namespace Platform.API.Modules.Accounts;

/// <summary>Login identity for every role. Email is unique per tenant (same person may use two venues).</summary>
public sealed class User : AuditableEntity
{
    public string Email { get; set; } = "";
    public string NormalizedEmail { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Role { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public int FailedLoginCount { get; set; }
    public DateTime? LockoutEnd { get; set; }
    public DateTime? LastLoginAt { get; set; }
}

/// <summary>Only a SHA-256 hash of the token is stored. Tokens rotate on use and belong to a "family".</summary>
public sealed class RefreshToken : BaseEntity
{
    public Guid UserId { get; set; }
    public string TokenHash { get; set; } = "";
    public Guid FamilyId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public string? CreatedByIp { get; set; }
    public DateTime? RevokedAt { get; set; }
    public string? ReplacedByTokenHash { get; set; }
}

/// <summary>Security-relevant events (logins, lockouts, role/staff changes). Never store secrets here.</summary>
public sealed class AuditLog : BaseEntity
{
    public Guid? TenantId { get; set; }
    public Guid? UserId { get; set; }
    public string Action { get; set; } = "";
    public string? IpAddress { get; set; }
    public string? Detail { get; set; }
    public DateTime OccurredAt { get; set; }
}

public static class AuditActions
{
    public const string Register = "auth.register";
    public const string LoginSuccess = "auth.login.success";
    public const string LoginFailed = "auth.login.failed";
    public const string Lockout = "auth.lockout";
    public const string RefreshReuse = "auth.refresh.reuse_detected";
    public const string PasswordChanged = "auth.password.changed";
    public const string Logout = "auth.logout";
    public const string TenantCreated = "tenant.created";
    public const string TenantStatusChanged = "tenant.status_changed";
    public const string StaffCreated = "staff.created";
    public const string StaffDeactivated = "staff.deactivated";
    public const string LoyaltyAward = "loyalty.award";
    public const string LoyaltyRedeem = "loyalty.redeem";
    public const string CustomerErased = "customer.erased";
}
