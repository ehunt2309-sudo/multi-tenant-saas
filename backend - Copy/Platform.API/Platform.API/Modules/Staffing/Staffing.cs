using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Platform.API.Core.Entities;
using Platform.API.Core.Interfaces;
using Platform.API.Infrastructure.Persistence;
using Platform.API.Infrastructure.Security;
using Platform.API.Modules.Accounts;
using Platform.API.Shared.Constants;
using Platform.API.Shared.Exceptions;
using Platform.API.Shared.Utilities;

namespace Platform.API.Modules.Staffing;

public sealed class StaffMember : AuditableEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public string JobTitle { get; set; } = "";
    public decimal HourlyRate { get; set; }          // sensitive: managers (and the person themselves) only
    public bool IsActive { get; set; } = true;
}

public sealed record StaffDto(Guid Id, string FirstName, string LastName, string Email, string Role,
    string JobTitle, bool IsActive, decimal? HourlyRate);

/// <summary>Safe for the public booking page: no email, no pay rate.</summary>
public sealed record PublicStaffDto(Guid Id, string Name, string JobTitle);

public sealed record CreateStaffRequest(
    [property: Required, EmailAddress, StringLength(256)] string Email,
    [property: Required, StringLength(128, MinimumLength = 12)] string Password,
    [property: Required, StringLength(100)] string FirstName,
    [property: Required, StringLength(100)] string LastName,
    [property: Required, StringLength(100)] string JobTitle,
    [property: Range(0, 100000)] decimal HourlyRate,
    [property: Required] string Role);

public sealed record UpdateStaffRequest(
    [property: Required, StringLength(100)] string FirstName,
    [property: Required, StringLength(100)] string LastName,
    [property: Required, StringLength(100)] string JobTitle,
    [property: Range(0, 100000)] decimal HourlyRate,
    bool IsActive);

public static class StaffQueries
{
    public static async Task<Guid> CurrentStaffIdAsync(this AppDbContext db, ICurrentUser user, CancellationToken ct)
    {
        var uid = user.UserId ?? throw new UnauthorizedException();
        var id = await db.StaffMembers.Where(s => s.UserId == uid && s.IsActive).Select(s => (Guid?)s.Id).FirstOrDefaultAsync(ct);
        return id ?? throw new ForbiddenException("No active staff profile for this account.");
    }
}

public sealed class StaffService(AppDbContext db, ICurrentUser currentUser, PasswordService passwords, AuditService audit)
{
    public async Task<IReadOnlyList<StaffDto>> ListAsync(CancellationToken ct)
    {
        var isManager = currentUser.Role == Roles.Manager;
        return await db.StaffMembers.AsNoTracking()
            .OrderBy(s => s.User.LastName).ThenBy(s => s.User.FirstName).Take(500)
            .Select(s => new StaffDto(s.Id, s.User.FirstName, s.User.LastName, s.User.Email, s.User.Role,
                s.JobTitle, s.IsActive, isManager ? s.HourlyRate : (decimal?)null))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<PublicStaffDto>> ListPublicAsync(CancellationToken ct) =>
        await db.StaffMembers.AsNoTracking().Where(s => s.IsActive && s.User.Role == Roles.Staff)
            .OrderBy(s => s.User.FirstName).Take(200)
            .Select(s => new PublicStaffDto(s.Id, s.User.FirstName + " " + s.User.LastName, s.JobTitle))
            .ToListAsync(ct);

    public async Task<StaffDto> CreateAsync(CreateStaffRequest r, string? ip, CancellationToken ct)
    {
        if (!Roles.IsTenantStaffRole(r.Role)) throw new BadRequestException("Role must be Staff or Manager.");

        var email = r.Email.Trim();
        var normalized = TextHelper.NormalizeEmail(email);
        passwords.EnforcePolicy(r.Password, email);

        if (await db.Users.AnyAsync(u => u.NormalizedEmail == normalized, ct))
            throw new ConflictException("A user with this email already exists.");

        var user = new User
        {
            Email = email,
            NormalizedEmail = normalized,
            FirstName = r.FirstName.Trim(),
            LastName = r.LastName.Trim(),
            Role = r.Role
        };
        user.PasswordHash = passwords.Hash(user, r.Password);

        var staff = new StaffMember { UserId = user.Id, JobTitle = r.JobTitle.Trim(), HourlyRate = r.HourlyRate };
        db.AddRange(user, staff);
        audit.Record(AuditActions.StaffCreated, currentUser.UserId, currentUser.TenantId, ip, $"{r.Role} {user.Id}");
        await db.SaveChangesAsync(ct);

        return new StaffDto(staff.Id, user.FirstName, user.LastName, user.Email, user.Role, staff.JobTitle, true, staff.HourlyRate);
    }

    public async Task<StaffDto> UpdateAsync(Guid id, UpdateStaffRequest r, string? ip, CancellationToken ct)
    {
        var staff = await db.StaffMembers.Include(s => s.User).FirstOrDefaultAsync(s => s.Id == id, ct)
                    ?? throw new NotFoundException("Staff member not found.");

        if (!r.IsActive && staff.UserId == currentUser.UserId)
            throw new BadRequestException("You cannot deactivate your own account.");

        staff.User.FirstName = r.FirstName.Trim();
        staff.User.LastName = r.LastName.Trim();
        staff.JobTitle = r.JobTitle.Trim();
        staff.HourlyRate = r.HourlyRate;

        if (staff.IsActive && !r.IsActive)
        {
            staff.IsActive = false;
            staff.User.IsActive = false;
            var now = DateTime.UtcNow;
            await db.RefreshTokens.Where(t => t.UserId == staff.UserId && t.RevokedAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now), ct);
            audit.Record(AuditActions.StaffDeactivated, currentUser.UserId, currentUser.TenantId, ip, $"user {staff.UserId}");
        }
        else if (!staff.IsActive && r.IsActive)
        {
            staff.IsActive = true;
            staff.User.IsActive = true;
        }

        await db.SaveChangesAsync(ct);
        return new StaffDto(staff.Id, staff.User.FirstName, staff.User.LastName, staff.User.Email, staff.User.Role,
            staff.JobTitle, staff.IsActive, staff.HourlyRate);
    }
}
