namespace Platform.API.Shared.Constants;

public static class Roles
{
    public const string PlatformAdmin = "PlatformAdmin"; // you (the SaaS operator)
    public const string Manager = "Manager";             // client business owner/manager
    public const string Staff = "Staff";                 // client employee
    public const string Customer = "Customer";           // end customer of a client business

    public static bool IsTenantStaffRole(string role) => role is Manager or Staff;
}

public static class Policies
{
    public const string PlatformAdminOnly = nameof(PlatformAdminOnly);
    public const string ManagerOnly = nameof(ManagerOnly);
    public const string StaffOrManager = nameof(StaffOrManager);
    public const string CustomerOnly = nameof(CustomerOnly);
    public const string AnyTenantUser = nameof(AnyTenantUser);
}

public static class CustomClaims
{
    public const string Subject = "sub";
    public const string TenantId = "tid";
    public const string Role = "role";
}

public static class PlatformConstants
{
    /// <summary>Internal tenant that owns PlatformAdmin accounts. Never exposed to clients.</summary>
    public static readonly Guid PlatformTenantId = new("00000000-0000-0000-0000-000000000001");
}
