using Microsoft.EntityFrameworkCore;
using Platform.API.Infrastructure.Persistence;
using Platform.API.Modules.Bookings;
using Platform.API.Shared.Exceptions;

namespace Platform.API.Modules.Analytics;

public sealed record SummaryDto(DateTime From, DateTime To, int TotalBookings, IReadOnlyDictionary<string, int> BookingsByStatus,
    decimal Revenue, int NewCustomers, int UniqueCustomersBooked);

public sealed record TopCustomerDto(Guid CustomerId, string Name, decimal TotalSpend, int VisitCount, DateTime? LastVisitAt);
public sealed record DailyRevenueDto(DateTime Day, decimal Revenue, int CompletedBookings);
public sealed record DemographicRowDto(string AgeBand, string Gender, int Customers, decimal TotalSpend, decimal AverageVisits);

public sealed class AnalyticsService(AppDbContext db)
{
    private const int MaxDays = 366;

    private static (DateTime f, DateTime t) Range(DateTimeOffset from, DateTimeOffset to)
    {
        var f = from.UtcDateTime; var t = to.UtcDateTime;
        if (t <= f) throw new BadRequestException("'to' must be after 'from'.");
        if ((t - f).TotalDays > MaxDays) throw new BadRequestException($"Range cannot exceed {MaxDays} days.");
        return (f, t);
    }

    public async Task<SummaryDto> SummaryAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct)
    {
        var (f, t) = Range(from, to);
        var inRange = db.Bookings.AsNoTracking().Where(b => b.StartsAt >= f && b.StartsAt < t);

        var byStatus = await inRange.GroupBy(b => b.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() }).ToListAsync(ct);

        var revenue = await inRange.Where(b => b.Status == BookingStatus.Completed).SumAsync(b => b.Price, ct);
        var newCustomers = await db.Customers.CountAsync(c => c.CreatedAt >= f && c.CreatedAt < t, ct);
        var unique = await inRange.Where(b => b.Status != BookingStatus.Cancelled)
            .Select(b => b.CustomerId).Distinct().CountAsync(ct);

        return new SummaryDto(f, t, byStatus.Sum(x => x.Count),
            byStatus.ToDictionary(x => x.Status.ToString(), x => x.Count), revenue, newCustomers, unique);
    }

    public async Task<IReadOnlyList<DailyRevenueDto>> RevenueByDayAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct)
    {
        var (f, t) = Range(from, to);
        // Days are UTC. Convert with the tenant's TimeZone in the frontend if you need local days.
        return await db.Bookings.AsNoTracking()
            .Where(b => b.Status == BookingStatus.Completed && b.StartsAt >= f && b.StartsAt < t)
            .GroupBy(b => b.StartsAt.Date)
            .Select(g => new DailyRevenueDto(g.Key, g.Sum(x => x.Price), g.Count()))
            .OrderBy(x => x.Day).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<TopCustomerDto>> TopCustomersAsync(int take, CancellationToken ct) =>
        await db.Customers.AsNoTracking().Where(c => !c.IsAnonymized && c.TotalSpend > 0)
            .OrderByDescending(c => c.TotalSpend).Take(Math.Clamp(take, 1, 100))
            .Select(c => new TopCustomerDto(c.Id, c.FirstName + " " + c.LastName, c.TotalSpend, c.VisitCount, c.LastVisitAt))
            .ToListAsync(ct);

    /// <summary>Who are my customers? Aggregated by age band and gender only - no individual PII returned.</summary>
    public async Task<IReadOnlyList<DemographicRowDto>> DemographicsAsync(CancellationToken ct)
    {
        var rows = await db.Customers.AsNoTracking().Where(c => !c.IsAnonymized)
            .Select(c => new { c.DateOfBirth, c.Gender, c.TotalSpend, c.VisitCount }).ToListAsync(ct);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return rows
            .GroupBy(r => (Band: AgeBand(r.DateOfBirth, today), Gender: string.IsNullOrWhiteSpace(r.Gender) ? "Unspecified" : r.Gender!))
            .Select(g => new DemographicRowDto(g.Key.Band, g.Key.Gender, g.Count(), g.Sum(x => x.TotalSpend),
                Math.Round((decimal)g.Average(x => x.VisitCount), 2)))
            .OrderBy(r => r.AgeBand).ThenBy(r => r.Gender).ToList();
    }

    private static string AgeBand(DateOnly? dob, DateOnly today)
    {
        if (dob is null) return "Unknown";
        var age = today.Year - dob.Value.Year;
        if (dob.Value > today.AddYears(-age)) age--;
        return age switch
        {
            < 18 => "Under 18",
            < 25 => "18-24",
            < 35 => "25-34",
            < 45 => "35-44",
            < 55 => "45-54",
            < 65 => "55-64",
            _ => "65+"
        };
    }
}
