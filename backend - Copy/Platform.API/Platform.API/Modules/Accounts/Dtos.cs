using System.ComponentModel.DataAnnotations;

namespace Platform.API.Modules.Accounts;

public sealed record RegisterRequest(
    [property: Required, EmailAddress, StringLength(256)] string Email,
    [property: Required, StringLength(128, MinimumLength = 12)] string Password,
    [property: Required, StringLength(100)] string FirstName,
    [property: Required, StringLength(100)] string LastName,
    [property: StringLength(32)] string? Phone,
    DateOnly? DateOfBirth,
    [property: StringLength(32)] string? Gender,
    bool MarketingConsent);

public sealed record LoginRequest(
    [property: Required, EmailAddress, StringLength(256)] string Email,
    [property: Required, StringLength(128)] string Password);

public sealed record RefreshRequest([property: Required, StringLength(200)] string RefreshToken);

public sealed record ChangePasswordRequest(
    [property: Required, StringLength(128)] string CurrentPassword,
    [property: Required, StringLength(128, MinimumLength = 12)] string NewPassword);

public sealed record UserInfo(Guid Id, string Email, string FirstName, string LastName, string Role, Guid TenantId);

public sealed record AuthResponse(
    string AccessToken,
    DateTime AccessTokenExpiresAt,
    string RefreshToken,
    DateTime RefreshTokenExpiresAt,
    UserInfo User);
