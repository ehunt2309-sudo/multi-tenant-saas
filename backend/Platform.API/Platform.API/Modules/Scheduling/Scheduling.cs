using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Platform.API.Core.Entities;
using Platform.API.Core.Interfaces;
using Platform.API.Infrastructure.Persistence;
using Platform.API.Modules.Staffing;
using Platform.API.Shared.Exceptions;

namespace Platform.API.Modules.Scheduling;

public sealed class Shift : AuditableEntity
{
    public Guid StaffMemberId { get; set; }
    public StaffMember StaffMember { get; set; } = null!;
    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }
    public DateTime? ClockInAt { get; set; }
    public DateTime? ClockOutAt { get; set; }
    public string? Notes { get; set; }
}

public sealed record ShiftDto(Guid Id, Guid StaffMemberId, string StaffName, DateTime StartsAt, DateTime EndsAt,
    DateTime? ClockInAt, DateTime? ClockOutAt, string? Notes);

public sealed record CreateShiftRequest(
    Guid StaffMemberId, DateTimeOffset StartsAt, DateTimeOffset EndsAt, [StringLength(500)] string? Notes);

public sealed record UpdateShiftRequest(
    DateTimeOffset StartsAt, DateTimeOffset EndsAt, [StringLength(500)] string? Notes);

public sealed record HoursReportRow(Guid StaffMemberId, string StaffName, decimal ScheduledHours, decimal WorkedHours, decimal Earned);

public sealed class ShiftService(AppDbContext db, ICurrentUser user)
{
    private static readonly TimeSpan ClockInEarlyWindow = TimeSpan.FromMinutes(30);
    private const int MaxRangeDays = 62;

    private IQueryable<ShiftDto> Project(IQueryable<Shift> q) => q.Select(s => new ShiftDto(
        s.Id, s.StaffMemberId, s.StaffMember.User.FirstName + " " + s.StaffMember.User.LastName,
        s.StartsAt, s.EndsAt, s.ClockInAt, s.ClockOutAt, s.Notes));

    private static (DateTime from, DateTime to) Range(DateTimeOffset from, DateTimeOffset to)
    {
        var f = from.UtcDateTime; var t = to.UtcDateTime;
        if (t <= f) throw new BadRequestException("'to' must be after 'from'.");
        if ((t - f).TotalDays > MaxRangeDays) throw new BadRequestException($"Range cannot exceed {MaxRangeDays} days.");
        return (f, t);
    }

    private static void ValidateTimes(DateTime start, DateTime end)
    {
        if (end <= start) throw new BadRequestException("Shift end must be after start.");
        if ((end - start).TotalHours > 24) throw new BadRequestException("A shift cannot exceed 24 hours.");
    }

    private async Task EnsureNoOverlapAsync(Guid staffId, DateTime start, DateTime end, Guid? ignoreId, CancellationToken ct)
    {
        if (await db.Shifts.AnyAsync(s => s.StaffMemberId == staffId && s.Id != ignoreId && s.StartsAt < end && s.EndsAt > start, ct))
            throw new ConflictException("This staff member already has an overlapping shift.");
    }

    public async Task<IReadOnlyList<ShiftDto>> ListAsync(DateTimeOffset from, DateTimeOffset to, Guid? staffId, CancellationToken ct)
    {
        var (f, t) = Range(from, to);
        return await Project(db.Shifts.AsNoTracking()
                .Where(s => s.StartsAt >= f && s.StartsAt < t && (staffId == null || s.StaffMemberId == staffId))
                .OrderBy(s => s.StartsAt).Take(2000))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ShiftDto>> MineAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct)
    {
        var me = await db.CurrentStaffIdAsync(user, ct);
        return await ListAsync(from, to, me, ct);
    }

    public async Task<ShiftDto> CreateAsync(CreateShiftRequest r, CancellationToken ct)
    {
        var start = r.StartsAt.UtcDateTime; var end = r.EndsAt.UtcDateTime;
        ValidateTimes(start, end);
        if (!await db.StaffMembers.AnyAsync(s => s.Id == r.StaffMemberId && s.IsActive, ct))
            throw new NotFoundException("Staff member not found.");
        await EnsureNoOverlapAsync(r.StaffMemberId, start, end, null, ct);

        var shift = new Shift { StaffMemberId = r.StaffMemberId, StartsAt = start, EndsAt = end, Notes = r.Notes?.Trim() };
        db.Shifts.Add(shift);
        await db.SaveChangesAsync(ct);
        return await GetAsync(shift.Id, ct);
    }

    public async Task<ShiftDto> UpdateAsync(Guid id, UpdateShiftRequest r, CancellationToken ct)
    {
        var start = r.StartsAt.UtcDateTime; var end = r.EndsAt.UtcDateTime;
        ValidateTimes(start, end);
        var shift = await db.Shifts.FirstOrDefaultAsync(s => s.Id == id, ct) ?? throw new NotFoundException("Shift not found.");
        if (shift.ClockInAt is not null) throw new ConflictException("A shift that has been worked can no longer be edited.");
        await EnsureNoOverlapAsync(shift.StaffMemberId, start, end, id, ct);

        shift.StartsAt = start; shift.EndsAt = end; shift.Notes = r.Notes?.Trim();
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var shift = await db.Shifts.FirstOrDefaultAsync(s => s.Id == id, ct) ?? throw new NotFoundException("Shift not found.");
        if (shift.ClockInAt is not null) throw new ConflictException("A shift that has been worked cannot be deleted.");
        db.Shifts.Remove(shift);
        await db.SaveChangesAsync(ct);
    }

    public async Task<ShiftDto> ClockInAsync(Guid id, CancellationToken ct)
    {
        var shift = await OwnShiftAsync(id, ct);
        var now = DateTime.UtcNow;
        if (shift.ClockInAt is not null) throw new ConflictException("Already clocked in.");
        if (now < shift.StartsAt - ClockInEarlyWindow || now > shift.EndsAt)
            throw new BadRequestException("You can only clock in from 30 minutes before the shift until it ends.");
        shift.ClockInAt = now;
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<ShiftDto> ClockOutAsync(Guid id, CancellationToken ct)
    {
        var shift = await OwnShiftAsync(id, ct);
        if (shift.ClockInAt is null) throw new BadRequestException("You have not clocked in.");
        if (shift.ClockOutAt is not null) throw new ConflictException("Already clocked out.");
        shift.ClockOutAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<IReadOnlyList<HoursReportRow>> HoursAsync(DateTimeOffset from, DateTimeOffset to, Guid? staffId, CancellationToken ct)
    {
        var (f, t) = Range(from, to);
        var rows = await db.Shifts.AsNoTracking()
            .Where(s => s.StartsAt >= f && s.StartsAt < t && (staffId == null || s.StaffMemberId == staffId))
            .Select(s => new
            {
                s.StaffMemberId,
                Name = s.StaffMember.User.FirstName + " " + s.StaffMember.User.LastName,
                Rate = s.StaffMember.HourlyRate,
                s.StartsAt, s.EndsAt, s.ClockInAt, s.ClockOutAt
            }).ToListAsync(ct);

        return rows.GroupBy(r => new { r.StaffMemberId, r.Name, r.Rate })
            .Select(g =>
            {
                var scheduled = g.Sum(x => (decimal)(x.EndsAt - x.StartsAt).TotalHours);
                var worked = g.Where(x => x.ClockInAt != null && x.ClockOutAt != null)
                    .Sum(x => (decimal)(x.ClockOutAt!.Value - x.ClockInAt!.Value).TotalHours);
                return new HoursReportRow(g.Key.StaffMemberId, g.Key.Name, Math.Round(scheduled, 2),
                    Math.Round(worked, 2), Math.Round(worked * g.Key.Rate, 2));
            })
            .OrderBy(r => r.StaffName).ToList();
    }

    public async Task<IReadOnlyList<HoursReportRow>> MyHoursAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct) =>
        await HoursAsync(from, to, await db.CurrentStaffIdAsync(user, ct), ct);

    private async Task<Shift> OwnShiftAsync(Guid id, CancellationToken ct)
    {
        var me = await db.CurrentStaffIdAsync(user, ct);
        // Another person's shift looks identical to a missing one (no existence leak).
        return await db.Shifts.FirstOrDefaultAsync(s => s.Id == id && s.StaffMemberId == me, ct)
               ?? throw new NotFoundException("Shift not found.");
    }

    private async Task<ShiftDto> GetAsync(Guid id, CancellationToken ct) =>
        await Project(db.Shifts.AsNoTracking().Where(s => s.Id == id)).FirstAsync(ct);
}

