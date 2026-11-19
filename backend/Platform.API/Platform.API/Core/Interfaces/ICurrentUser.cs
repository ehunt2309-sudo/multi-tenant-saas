namespace Platform.API.Core.Interfaces;

/// <summary>The authenticated caller, read from validated JWT claims.</summary>
public interface ICurrentUser
{
    bool IsAuthenticated { get; }
    Guid? UserId { get; }
    Guid? TenantId { get; }
    string? Role { get; }
}
