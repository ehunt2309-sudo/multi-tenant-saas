using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Platform.API.Modules.Accounts;
using Platform.API.Shared.Exceptions;

namespace Platform.API.Infrastructure.Security;

/// <summary>PBKDF2-HMAC-SHA512 (ASP.NET Identity v3 format) with a raised iteration count + upgrade-on-login.</summary>
public sealed class PasswordService
{
    private readonly PasswordHasher<User> _hasher =
        new(Options.Create(new PasswordHasherOptions { IterationCount = 210_000 }));

    private readonly string _dummyHash;

    public PasswordService()
    {
        _dummyHash = _hasher.HashPassword(new User(), Guid.NewGuid().ToString("N"));
    }

    public string Hash(User user, string password) => _hasher.HashPassword(user, password);

    public PasswordVerificationResult Verify(User user, string password) =>
        _hasher.VerifyHashedPassword(user, user.PasswordHash, password);

    /// <summary>Spend the same CPU time when the account doesn't exist so response time doesn't reveal it.</summary>
    public void BurnTime() => _hasher.VerifyHashedPassword(new User(), _dummyHash, "not-the-password");

    public void EnforcePolicy(string password, string? email)
    {
        if (password.Length < 12 || password.Length > 128)
            throw new BadRequestException("Password must be between 12 and 128 characters.");

        if (!string.IsNullOrWhiteSpace(email))
        {
            var local = email.Split('@')[0];
            if (local.Length >= 4 && password.Contains(local, StringComparison.OrdinalIgnoreCase))
                throw new BadRequestException("Password must not contain your email name.");
        }

        if (password.Distinct().Count() < 5)
            throw new BadRequestException("Password is too repetitive.");
    }
}
