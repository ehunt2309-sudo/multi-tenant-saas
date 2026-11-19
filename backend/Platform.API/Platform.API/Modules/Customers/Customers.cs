using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Platform.API.Core.Entities;
using Platform.API.Core.Interfaces;
using Platform.API.Infrastructure.Persistence;
using Platform.API.Modules.Accounts;
using Platform.API.Modules.Loyalty;
using Platform.API.Shared.Exceptions;
using Platform.API.Shared.Responses;
using Platform.API.Shared.Utilities;

namespace Platform.API.Modules.Customers;

public sealed class Customer : AuditableEntity
{
    public Guid? UserId { get; set; }                 // null = walk-in customer without an online account
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public string? Gender { get; set; }
    public bool MarketingConsent { get; set; }        // POPIA: explicit, timestamped consent
    public DateTime? ConsentUpdatedAt { get; set; }
    public decimal TotalSpend { get; set; }
    public int VisitCount { get; set; }
    public DateTime? LastVisitAt { get; set; }
    public bool IsAnonymized { get; set; }
}

public sealed record CustomerDto(
    Guid Id, string FirstName, string LastName, string? Email, string? Phone, DateOnly? DateOfBirth,
    string? Gender, bool MarketingConsent, decimal TotalSpend, int VisitCount, DateTime? LastVisitAt);

public sealed record CreateCustomerRequest(
    [property: Required, StringLength(100)] string FirstName,
    [property: Required, StringLength(100)] string LastName,
    [property: EmailAddress, StringLength(256)] string? Email,
    [property: StringLength(32)] string? Phone,
    DateOnly? DateOfBirth,
    [property: StringLength(32)] string? Gender,
    bool MarketingConsent);

public sealed record UpdateCustomerRequest(
    [property: Required, StringLength(100)] string FirstName,
    [property: Required, StringLength(100)] string LastName,
    [property: StringLength(32)] string? Phone,
    DateOnly? DateOfBirth,
    [property: StringLength(32)] string? Gender,
    bool MarketingConsent);

public static class CustomerRules
{
    public static void ValidateDateOfBirth(DateOnly? dob)
    {
        if (dob is null) return;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (dob > today || dob < today.AddYears(-120))
            throw new BadRequestException("Date of birth is not valid.");
    }

    /// <summary>Resolves the Customer row for the logged-in customer account.</summary>
    public static async Task<Guid> CurrentCustomerIdAsync(this AppDbContext db, ICurrentUser user, CancellationToken ct)
    {
        var uid = user.UserId ?? throw new UnauthorizedException();
        var id = await db.Customers.Where(c => c.UserId == uid).Select(c => (Guid?)c.Id).FirstOrDefaultAsync(ct);
        return id ?? throw new ForbiddenException("No customer profile exists for this account.");
    }
}

public sealed class CustomerService(AppDbContext db, ICurrentUser user, AuditService audit)
{
    private static CustomerDto ToDto(Customer c) => new(c.Id, c.FirstName, c.LastName, c.Email, c.Phone,
        c.DateOfBirth, c.Gender, c.MarketingConsent, c.TotalSpend, c.VisitCount, c.LastVisitAt);

    public Task<PagedResult<CustomerDto>> ListAsync(string? search, int page, int pageSize, CancellationToken ct)
    {
        var q = db.Customers.AsNoTracking().Where(c => !c.IsAnonymized);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            q = q.Where(c => c.LastName.Contains(s) || c.FirstName.Contains(s) || (c.Email != null && c.Email.Contains(s)));
        }
        return q.OrderBy(c => c.LastName).ThenBy(c => c.FirstName)
            .Select(c => new CustomerDto(c.Id, c.FirstName, c.LastName, c.Email, c.Phone, c.DateOfBirth,
                c.Gender, c.MarketingConsent, c.TotalSpend, c.VisitCount, c.LastVisitAt))
            .ToPagedAsync(page, pageSize, ct);
    }

    public async Task<CustomerDto> GetAsync(Guid id, CancellationToken ct) =>
        ToDto(await db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, ct)
              ?? throw new NotFoundException("Customer not found."));

    public async Task<CustomerDto> GetMeAsync(CancellationToken ct)
    {
        var id = await db.CurrentCustomerIdAsync(user, ct);
        return await GetAsync(id, ct);
    }

    public async Task<CustomerDto> CreateAsync(CreateCustomerRequest r, CancellationToken ct)
    {
        CustomerRules.ValidateDateOfBirth(r.DateOfBirth);
        var customer = new Customer
        {
            FirstName = r.FirstName.Trim(),
            LastName = r.LastName.Trim(),
            Email = r.Email?.Trim(),
            Phone = r.Phone?.Trim(),
            DateOfBirth = r.DateOfBirth,
            Gender = r.Gender?.Trim(),
            MarketingConsent = r.MarketingConsent,
            ConsentUpdatedAt = DateTime.UtcNow
        };
        db.Customers.Add(customer);
        db.LoyaltyAccounts.Add(new LoyaltyAccount { CustomerId = customer.Id });
        await db.SaveChangesAsync(ct);
        return ToDto(customer);
    }

    public async Task<CustomerDto> UpdateAsync(Guid id, UpdateCustomerRequest r, CancellationToken ct)
    {
        CustomerRules.ValidateDateOfBirth(r.DateOfBirth);
        var c = await db.Customers.FirstOrDefaultAsync(x => x.Id == id && !x.IsAnonymized, ct)
                ?? throw new NotFoundException("Customer not found.");

        c.FirstName = r.FirstName.Trim();
        c.LastName = r.LastName.Trim();
        c.Phone = r.Phone?.Trim();
        c.DateOfBirth = r.DateOfBirth;
        c.Gender = r.Gender?.Trim();
        if (c.MarketingConsent != r.MarketingConsent)
        {
            c.MarketingConsent = r.MarketingConsent;
            c.ConsentUpdatedAt = DateTime.UtcNow;
        }
        await db.SaveChangesAsync(ct);
        return ToDto(c);
    }

    public async Task<CustomerDto> UpdateMeAsync(UpdateCustomerRequest r, CancellationToken ct) =>
        await UpdateAsync(await db.CurrentCustomerIdAsync(user, ct), r, ct);

    /// <summary>POPIA right-to-erasure: strips personal data but keeps anonymous aggregates for analytics.</summary>
    public async Task AnonymizeAsync(Guid id, string? ip, CancellationToken ct)
    {
        var c = await db.Customers.FirstOrDefaultAsync(x => x.Id == id, ct)
                ?? throw new NotFoundException("Customer not found.");
        if (c.IsAnonymized) return;

        if (c.UserId is { } uid)
        {
            var u = await db.Users.FirstOrDefaultAsync(x => x.Id == uid, ct);
            if (u is not null)
            {
                u.IsActive = false;
                u.Email = $"erased-{u.Id:N}@invalid";
                u.NormalizedEmail = TextHelper.NormalizeEmail(u.Email);
                u.FirstName = "Erased";
                u.LastName = "Erased";
                u.PasswordHash = "!";
                var now = DateTime.UtcNow;
                await db.RefreshTokens.Where(t => t.UserId == u.Id && t.RevokedAt == null)
                    .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now), ct);
            }
        }

        c.FirstName = "Erased";
        c.LastName = "Erased";
        c.Email = null;
        c.Phone = null;
        c.DateOfBirth = null;
        c.Gender = null;
        c.MarketingConsent = false;
        c.IsAnonymized = true;

        audit.Record(AuditActions.CustomerErased, user.UserId, user.TenantId, ip, $"customer {id}");
        await db.SaveChangesAsync(ct);
    }
}
