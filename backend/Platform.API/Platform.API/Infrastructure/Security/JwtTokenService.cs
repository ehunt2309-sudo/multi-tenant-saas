using System.Buffers.Text;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Platform.API.Modules.Accounts;
using Platform.API.Shared.Constants;

namespace Platform.API.Infrastructure.Security;

public sealed class JwtTokenService(IOptions<JwtOptions> options)
{
    private readonly JwtOptions _o = options.Value;
    private readonly JsonWebTokenHandler _handler = new();
    private readonly SigningCredentials _creds = new(
        new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.Value.SigningKey)),
        SecurityAlgorithms.HmacSha256);

    public (string Token, DateTime ExpiresAt) CreateAccessToken(User user)
    {
        var now = DateTime.UtcNow;
        var expires = now.AddMinutes(_o.AccessTokenMinutes);

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _o.Issuer,
            Audience = _o.Audience,
            IssuedAt = now,
            NotBefore = now,
            Expires = expires,
            SigningCredentials = _creds,
            // Keep claims minimal: no email/PII inside the token.
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(CustomClaims.Subject, user.Id.ToString()),
                new Claim(CustomClaims.TenantId, user.TenantId.ToString()),
                new Claim(CustomClaims.Role, user.Role),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            })
        };

        return (_handler.CreateToken(descriptor), expires);
    }

    public DateTime RefreshExpiry() => DateTime.UtcNow.AddDays(_o.RefreshTokenDays);

    /// <summary>512 bits from the OS CSPRNG, URL-safe.</summary>
    public static string NewRefreshToken() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(64));

    public static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
