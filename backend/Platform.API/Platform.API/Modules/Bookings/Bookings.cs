using System.ComponentModel.DataAnnotations;
using System.Data;
using Microsoft.EntityFrameworkCore;
using Platform.API.Core.Entities;
using Platform.API.Core.Interfaces;
using Platform.API.Infrastructure.Persistence;
using Platform.API.Modules.Catalog;
using Platform.API.Modules.Customers;
using Platform.API.Modules.Loyalty;
using Platform.API.Modules.Staffing;
using Platform.API.Shared.Constants;
using Platform.API.Shared.Exceptions;
using Platform.API.Shared.Responses;

namespace Platform.API.Modules.Bookings;

public enum BookingStatus { Pending, Confirmed, Completed, Cancelled, NoShow }

public sealed class Booking : AuditableEntity
{
    public Guid CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    public Guid? StaffMemberId { get; set; }
    public StaffMember? StaffMember { get; set; }
    public Guid ServiceOfferingId { get; set; }
    public ServiceOffering ServiceOffering { get; set; } = null!;
    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }
    public BookingStatus Status { get; set; }
    public int PartySize { get; set; } = 1;
    public decimal Price { get; set; }               // snapshot of the service price at booking time
    public string? Notes { get; set; }
    public byte[] RowVersion { get; set; } = [];
}

public sealed record BookingDto(
    Guid Id, Guid CustomerId, string CustomerName, Guid? StaffMemberId, string? StaffName,
    Guid ServiceOfferingId, string ServiceName, DateTime StartsAt, DateTime EndsAt,
    BookingStatus Status, int PartySize, decimal Price, string? Notes);

public sealed record CreateBookingRequest(
    Guid ServiceOfferingId,
    Guid? StaffMemberId,
    DateTimeOffset StartsAt,
    [property: Range(1, 50)] int PartySize,
    [property: StringLength(500)] string? Notes,
    Guid? CustomerId);          // only used when staff/manager books on behalf of a customer

public sealed record UpdateBookingStatusRequest(BookingStatus Status);

public sealed class BookingService(AppDbContext db, ICurrentUser user, ITenantProvider tenant, LoyaltyService loyalty)
{
    private static readonly TimeSpan CustomerCancelNotice = TimeSpan.FromHours(24);

    private static IQueryable<BookingDto> Project(IQueryable<Booking> q) => q.Select(b => new BookingDto(
        b.Id, b.CustomerId, b.Customer.FirstName + " " + b.Customer.LastName,
        b.StaffMemberId, b.StaffMember == null ? null : b.StaffMember.User.FirstName + " " + b.StaffMember.User.LastName,
        b.ServiceOfferingId, b.ServiceOffering.Name, b.StartsAt, b.EndsAt, b.Status, b.PartySize, b.Price, b.Notes));

    // ---------- create ----------
    public async Task<BookingDto> CreateAsync(CreateBookingRequest r, CancellationToken ct)
    {
        var isCustomer = user.Role == Roles.Customer;
        Guid customerId;
        if (isCustomer)
        {
            customerId = await db.CurrentCustomerIdAsync(user, ct);
            if (r.CustomerId is not null && r.CustomerId != customerId) throw new ForbiddenException();
        }
        else
        {
            customerId = r.CustomerId ?? throw new BadRequestException("CustomerId is required.");
            if (!await db.Customers.AnyAsync(c => c.Id == customerId && !c.IsAnonymized, ct))
                throw new NotFoundException("Customer not found.");
        }

        var service = await db.ServiceOfferings.AsNoTracking().FirstOrDefaultAsync(s => s.Id == r.ServiceOfferingId && s.IsActive, ct)
                      ?? throw new NotFoundException("Service not found.");

        var now = DateTime.UtcNow;
        var start = r.StartsAt.UtcDateTime;
        if (isCustomer && start <= now) throw new BadRequestException("Bookings must be in the future.");
        if (start > now.AddYears(1)) throw new BadRequestException("Bookings can be made up to one year ahead.");
        var end = start.AddMinutes(service.DurationMinutes);

        if (r.StaffMemberId is { } sid && !await db.StaffMembers.AnyAsync(s => s.Id == sid && s.IsActive, ct))
            throw new NotFoundException("Staff member not found.");

        var booking = new Booking
        {
            CustomerId = customerId,
            StaffMemberId = r.StaffMemberId,
            ServiceOfferingId = service.Id,
            StartsAt = start,
            EndsAt = end,
            PartySize = r.PartySize,
            Price = service.Price,
            Notes = r.Notes?.Trim(),
            Status = isCustomer ? BookingStatus.Pending : BookingStatus.Confirmed
        };

        // Serializable + range check = two simultaneous requests can't both grab the same staff slot.
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);

            if (booking.StaffMemberId is { } staffId)
            {
                var clash = await db.Bookings.AnyAsync(b =>
                    b.StaffMemberId == staffId &&
                    (b.Status == BookingStatus.Pending || b.Status == BookingStatus.Confirmed) &&
                    b.StartsAt < end && b.EndsAt > start, ct);
                if (clash) throw new ConflictException("That time slot is no longer available.");
            }

            if (db.Entry(booking).State == EntityState.Detached) db.Bookings.Add(booking);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        });

        return await GetAsync(booking.Id, ct);
    }

    // ---------- read ----------
    public async Task<BookingDto> GetAsync(Guid id, CancellationToken ct)
    {
        var q = db.Bookings.AsNoTracking().Where(b => b.Id == id);
        if (user.Role == Roles.Customer)
        {
            var me = await db.CurrentCustomerIdAsync(user, ct);
            q = q.Where(b => b.CustomerId == me);   // someone else's booking == "not found"
        }
        return await Project(q).FirstOrDefaultAsync(ct) ?? throw new NotFoundException("Booking not found.");
    }

    public Task<PagedResult<BookingDto>> ListAsync(DateTimeOffset? from, DateTimeOffset? to, BookingStatus? status,
        Guid? staffId, int page, int pageSize, CancellationToken ct)
    {
        var q = db.Bookings.AsNoTracking().AsQueryable();
        if (from is not null) { var f = from.Value.UtcDateTime; q = q.Where(b => b.StartsAt >= f); }
        if (to is not null) { var t = to.Value.UtcDateTime; q = q.Where(b => b.StartsAt < t); }
        if (status is not null) q = q.Where(b => b.Status == status);
        if (staffId is not null) q = q.Where(b => b.StaffMemberId == staffId);
        return Project(q.OrderBy(b => b.StartsAt)).ToPagedAsync(page, pageSize, ct);
    }

    public async Task<PagedResult<BookingDto>> MineAsync(int page, int pageSize, CancellationToken ct)
    {
        var me = await db.CurrentCustomerIdAsync(user, ct);
        return await Project(db.Bookings.AsNoTracking().Where(b => b.CustomerId == me).OrderByDescending(b => b.StartsAt))
            .ToPagedAsync(page, pageSize, ct);
    }

    // ---------- state changes ----------
    public async Task<BookingDto> UpdateStatusAsync(Guid id, BookingStatus newStatus, CancellationToken ct)
    {
        var b = await db.Bookings.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Booking not found.");
        var now = DateTime.UtcNow;

        var allowed = (b.Status, newStatus) switch
        {
            (BookingStatus.Pending, BookingStatus.Confirmed) => true,
            (BookingStatus.Pending, BookingStatus.Cancelled) => true,
            (BookingStatus.Confirmed, BookingStatus.Completed) => true,
            (BookingStatus.Confirmed, BookingStatus.NoShow) => true,
            (BookingStatus.Confirmed, BookingStatus.Cancelled) => true,
            _ => false
        };
        if (!allowed) throw new ConflictException($"A {b.Status} booking cannot be changed to {newStatus}.");

        if (newStatus is BookingStatus.Completed or BookingStatus.NoShow && b.StartsAt > now)
            throw new BadRequestException("A booking that hasn't started yet cannot be completed or marked as a no-show.");

        b.Status = newStatus;

        if (newStatus == BookingStatus.Completed)
            await RecordCompletionAsync(b, now, ct);

        await db.SaveChangesAsync(ct);   // status + spend + points commit together or not at all
        return await GetAsync(id, ct);
    }

    public async Task<BookingDto> CancelAsync(Guid id, CancellationToken ct)
    {
        var q = db.Bookings.Where(x => x.Id == id);
        if (user.Role == Roles.Customer)
        {
            var me = await db.CurrentCustomerIdAsync(user, ct);
            q = q.Where(x => x.CustomerId == me);
        }
        var b = await q.FirstOrDefaultAsync(ct) ?? throw new NotFoundException("Booking not found.");

        if (b.Status is not (BookingStatus.Pending or BookingStatus.Confirmed))
            throw new ConflictException($"A {b.Status} booking cannot be cancelled.");

        if (user.Role == Roles.Customer && b.StartsAt - DateTime.UtcNow < CustomerCancelNotice)
            throw new BadRequestException("Online cancellation closes 24 hours before the booking. Please contact the venue.");

        b.Status = BookingStatus.Cancelled;
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    private async Task RecordCompletionAsync(Booking b, DateTime now, CancellationToken ct)
    {
        var customer = await db.Customers.FirstAsync(c => c.Id == b.CustomerId, ct);
        customer.TotalSpend += b.Price;
        customer.VisitCount += 1;
        customer.LastVisitAt = now;

        var t = await db.Tenants.AsNoTracking().FirstAsync(x => x.Id == tenant.TenantId!.Value, ct);
        var points = (int)Math.Floor(b.Price * t.LoyaltyPointsPerCurrencyUnit);
        if (points > 0)
            await loyalty.ApplyAsync(customer.Id, points, LoyaltyTransactionType.Earned, "Completed booking", b.Id, ct);
    }
}
