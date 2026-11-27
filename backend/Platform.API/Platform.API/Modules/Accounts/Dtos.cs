using System.ComponentModel.DataAnnotations;

namespace Platform.API.Modules.Accounts;

public sealed record RegisterRequest(
    [Required, EmailAddress, StringLength(256)] string Email,
    [Required, StringLength(128, MinimumLength = 12)] string Password,
    [Required, StringLength(100)] string FirstName,
    [Required, StringLength(100)] string LastName,
    [StringLength(32)] string? Phone,
    DateOnly? DateOfBirth,
    [StringLength(32)] string? Gender,
    bool MarketingConsent);

public sealed record LoginRequest(
    [Required, EmailAddress, StringLength(256)] string Email,
    [Required, StringLength(128)] string Password);

public sealed record RefreshRequest([Required, StringLength(200)] string RefreshToken);

public sealed record ChangePasswordRequest(
    [Required, StringLength(128)] string CurrentPassword,
    [Required, StringLength(128, MinimumLength = 12)] string NewPassword);

public sealed record UserInfo(Guid Id, string Email, string FirstName, string LastName, string Role, Guid TenantId);

public sealed record AuthResponse(
    string AccessToken,
    DateTime AccessTokenExpiresAt,
    string RefreshToken,
    DateTime RefreshTokenExpiresAt,
    UserInfo User);

