namespace MechanicShop.Infrastructure.Identity;

/// <summary>
/// The three roles the shop enforces. Kept as constants (not an enum) so they can be
/// seeded as ASP.NET Core Identity roles and used directly in authorization policies.
/// </summary>
public static class AppRoles
{
    public const string Manager = "Manager";

    public const string Technician = "Technician";

    public const string Customer = "Customer";

    /// <summary>Every role, used when seeding the Identity role table on first run.</summary>
    public static readonly IReadOnlyList<string> All = [Manager, Technician, Customer];
}
