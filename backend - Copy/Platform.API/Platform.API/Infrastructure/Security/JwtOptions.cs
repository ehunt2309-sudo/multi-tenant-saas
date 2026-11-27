using System.ComponentModel.DataAnnotations;

namespace Platform.API.Infrastructure.Security;

public sealed class JwtOptions
{
    public const string Section = "Jwt";

    [Required] public string Issuer { get; set; } = "";
    [Required] public string Audience { get; set; } = "";

    /// <summary>Secret. Comes from Key Vault (Jwt--SigningKey) or user-secrets locally, never from git.</summary>
    [Required, MinLength(64, ErrorMessage = "Jwt:SigningKey must be at least 64 characters.")]
    public string SigningKey { get; set; } = "";

    [Range(1, 60)] public int AccessTokenMinutes { get; set; } = 15;
    [Range(1, 90)] public int RefreshTokenDays { get; set; } = 14;
}
