using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Platform.API.Core.Interfaces;
using Platform.API.Infrastructure.Persistence;
using Platform.API.Infrastructure.Security;
using Platform.API.Modules.Customers;
using Platform.API.Modules.Loyalty;
using Platform.API.Shared.Constants;
using Platform.API.Shared.Exceptions;
using Platform.API.Shared.Utilities;

namespace Platform.API.Modules.Accounts;

public sealed class AuthService(
    AppDbContext db,
    ITenantProvider tenant,
    ICurrentUser currentUser,
    PasswordService passwords,
    JwtTokenService tokens,
    AuditService audit)
{
    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);
    private const string BadLogin = "Invalid email or password, or the account is temporarily locked.";
    private const string BadRefresh = "Session expired. Please sign in again.";

    // ---------- Customer self-registration (tenant comes from the request's origin) ----------
    public async Task<AuthResponse> RegisterCustomerAsync(RegisterRequest r, string? ip, CancellationToken ct)
    {
        var tenantId = tenant.TenantId ?? throw new BadRequestException("Tenant could not be resolved.");
        var email = r.Email.Trim();
        var normalized = TextHelper.NormalizeEmail(email);

        passwords.EnforcePolicy(r.Password, email);
        CustomerRules.ValidateDateOfBirth(r.DateOfBirth);

        if (await db.Users.AnyAsync(u => u.NormalizedEmail == normalized, ct))
            throw new ConflictException("An account with this email already exists.");

        var user = new User
        {
            Email = email,
            NormalizedEmail = normalized,
            FirstName = r.FirstName.Trim(),
            LastName = r.LastName.Trim(),
            Role = Roles.Customer
        };
        user.PasswordHash = passwords.Hash(user, r.Password);

        var customer = new Customer
        {
            UserId = user.Id,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Email = email,
            Phone = r.Phone?.Trim(),
            DateOfBirth = r.DateOfBirth,
            Gender = r.Gender?.Trim(),
            MarketingConsent = r.MarketingConsent,
            ConsentUpdatedAt = DateTime.UtcNow
        };

        db.AddRange(user, customer, new LoyaltyAccount { CustomerId = customer.Id });
        audit.Record(AuditActions.Register, user.Id, tenantId, ip);

        return await IssueTokensAsync(user, Guid.NewGuid(), ip, previous: null, ct);
    }

    // ---------- Login ----------
    public async Task<AuthResponse> LoginAsync(LoginRequest r, string? ip, CancellationToken ct)
    {
        var normalized = TextHelper.NormalizeEmail(r.Email);
        // No tenant resolved => this can only be a platform-admin login.
        var scope = tenant.TenantId ?? PlatformConstants.PlatformTenantId;

        var user = await db.Users.IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.TenantId == scope && u.NormalizedEmail == normalized, ct);

        if (user is null)
        {
            passwords.BurnTime();
            audit.Record(AuditActions.LoginFailed, null, scope, ip, "unknown account");
            await db.SaveChangesAsync(ct);
            throw new UnauthorizedException(BadLogin);
        }

        var now = DateTime.UtcNow;
        if (user.LockoutEnd is { } lockedUntil && lockedUntil > now)
        {
            passwords.BurnTime();
            audit.Record(AuditActions.LoginFailed, user.Id, scope, ip, "locked");
            await db.SaveChangesAsync(ct);
            throw new UnauthorizedException(BadLogin);
        }

        var result = passwords.Verify(user, r.Password);
        if (result == PasswordVerificationResult.Failed)
        {
            user.FailedLoginCount++;
            audit.Record(AuditActions.LoginFailed, user.Id, scope, ip, "bad password");
            if (user.FailedLoginCount >= MaxFailedAttempts)
            {
                user.LockoutEnd = now.Add(LockoutDuration);
                user.FailedLoginCount = 0;
                audit.Record(AuditActions.Lockout, user.Id, scope, ip);
            }
            await db.SaveChangesAsync(ct);
            throw new UnauthorizedException(BadLogin);
        }

        if (!user.IsActive)
        {
            audit.Record(AuditActions.LoginFailed, user.Id, scope, ip, "inactive");
            await db.SaveChangesAsync(ct);
            throw new UnauthorizedException(BadLogin);
        }

        if (result == PasswordVerificationResult.SuccessRehashNeeded)
            user.PasswordHash = passwords.Hash(user, r.Password);

        user.FailedLoginCount = 0;
        user.LockoutEnd = null;
        user.LastLoginAt = now;
        audit.Record(AuditActions.LoginSuccess, user.Id, user.TenantId, ip);

        return await IssueTokensAsync(user, Guid.NewGuid(), ip, previous: null, ct);
    }

    // ---------- Refresh (rotation + reuse detection) ----------
    public async Task<AuthResponse> RefreshAsync(string refreshToken, string? ip, CancellationToken ct)
    {
        var hash = JwtTokenService.Hash(refreshToken);
        var stored = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct)
                     ?? throw new UnauthorizedException(BadRefresh);

        if (stored.RevokedAt is not null)
        {
            // A rotated/revoked token was presented again: assume theft and kill the whole family.
            await RevokeFamilyAsync(stored.FamilyId, ct);
            audit.Record(AuditActions.RefreshReuse, stored.UserId, null, ip);
            await db.SaveChangesAsync(ct);
            throw new UnauthorizedException(BadRefresh);
        }

        if (stored.ExpiresAt <= DateTime.UtcNow)
            throw new UnauthorizedException(BadRefresh);

        var user = await db.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Id == stored.UserId, ct);
        if (user is null || !user.IsActive)
            throw new UnauthorizedException(BadRefresh);

        if (!await db.Tenants.AnyAsync(t => t.Id == user.TenantId && t.IsActive, ct))
            throw new UnauthorizedException(BadRefresh);   // suspended venues can't mint new tokens

        // A tenant user can only refresh from their own venue's origin (when one is resolved).
        if (tenant.TenantId is { } t && t != user.TenantId)
            throw new UnauthorizedException(BadRefresh);

        stored.RevokedAt = DateTime.UtcNow;
        return await IssueTokensAsync(user, stored.FamilyId, ip, stored, ct);
    }

    public async Task LogoutAsync(string refreshToken, string? ip, CancellationToken ct)
    {
        var hash = JwtTokenService.Hash(refreshToken);
        var stored = await db.RefreshTokens.AsNoTracking().FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (stored is null) return; // idempotent, reveals nothing

        await RevokeFamilyAsync(stored.FamilyId, ct);
        audit.Record(AuditActions.Logout, stored.UserId, null, ip);
        await db.SaveChangesAsync(ct);
    }

    public async Task ChangePasswordAsync(ChangePasswordRequest r, string? ip, CancellationToken ct)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedException();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct)
                   ?? throw new UnauthorizedException();

        if (passwords.Verify(user, r.CurrentPassword) == PasswordVerificationResult.Failed)
            throw new BadRequestException("Current password is incorrect.");

        passwords.EnforcePolicy(r.NewPassword, user.Email);
        user.PasswordHash = passwords.Hash(user, r.NewPassword);

        var now = DateTime.UtcNow;
        await db.RefreshTokens.Where(t => t.UserId == user.Id && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now), ct);

        audit.Record(AuditActions.PasswordChanged, user.Id, user.TenantId, ip);
        await db.SaveChangesAsync(ct);
    }

    public async Task<UserInfo> GetMeAsync(CancellationToken ct)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedException();
        return await db.Users.AsNoTracking().Where(u => u.Id == userId)
                   .Select(u => new UserInfo(u.Id, u.Email, u.FirstName, u.LastName, u.Role, u.TenantId))
                   .FirstOrDefaultAsync(ct)
               ?? throw new UnauthorizedException();
    }

    // ---------- helpers ----------
    private async Task<AuthResponse> IssueTokensAsync(User user, Guid familyId, string? ip, RefreshToken? previous, CancellationToken ct)
    {
        var (access, accessExpires) = tokens.CreateAccessToken(user);
        var raw = JwtTokenService.NewRefreshToken();
        var newHash = JwtTokenService.Hash(raw);
        var refreshExpires = tokens.RefreshExpiry();

        if (previous is not null) previous.ReplacedByTokenHash = newHash;

        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = newHash,
            FamilyId = familyId,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = refreshExpires,
            CreatedByIp = ip
        });

        await db.SaveChangesAsync(ct); // one atomic commit: user changes + audit + new token

        return new AuthResponse(access, accessExpires, raw, refreshExpires,
            new UserInfo(user.Id, user.Email, user.FirstName, user.LastName, user.Role, user.TenantId));
    }

    private Task<int> RevokeFamilyAsync(Guid familyId, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        return db.RefreshTokens.Where(t => t.FamilyId == familyId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now), ct);
    }
}
