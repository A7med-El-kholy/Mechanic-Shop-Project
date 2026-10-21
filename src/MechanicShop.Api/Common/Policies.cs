namespace MechanicShop.Api.Common;

/// <summary>
/// Named authorization policies. Defining them in ONE place keeps endpoint attributes
/// and tests in sync — scattered string literals drift silently.
/// </summary>
public static class Policies
{
    public const string IsManager = "IsManager";

    public const string IsTechnician = "IsTechnician";

    public const string IsCustomer = "IsCustomer";
}
