using System.Net.Mail;
using System.Text.RegularExpressions;

using MechanicShop.Domain.Common;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.Customers.Vehicles;

namespace MechanicShop.Domain.Customers;

public sealed class Customer : Entity
{
    public string Name { get; private set; } = string.Empty;
    public string PhoneNumber { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;

    // Identity user account linked to this customer profile. Null until the user
    // registers (or claims the profile); the Api layer enforces "customer sees own
    // records" through this link.
    public string? UserId { get; private set; }

    private readonly List<Vehicle> _vehicles = [];

    public IReadOnlyList<Vehicle> Vehicles => _vehicles.AsReadOnly();

    private Customer()
    { }

    private Customer(Guid id, string name, string phoneNumber, string email, List<Vehicle> vehicles)
        : base(id)
    {
        Name = name;
        PhoneNumber = phoneNumber;
        Email = email;

        // Defensive copy: the entity owns its collection; handing back the caller's
        // list would leak mutations both ways (an integration bug showed up exactly
        // this way in tests).
        _vehicles = [.. vehicles];
    }

    public static Result<Customer> Create(Guid id, string name, string phoneNumber, string email, List<Vehicle> vehicles)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return CustomerErrors.NameRequired;
        }

        if (string.IsNullOrWhiteSpace(phoneNumber) || !Regex.IsMatch(phoneNumber, @"^\+?\d{7,15}$"))
        {
            return CustomerErrors.InvalidPhoneNumber;
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            return CustomerErrors.EmailRequired;
        }

        try
        {
            _ = new MailAddress(email);
        }
        catch
        {
            return CustomerErrors.EmailInvalid;
        }

        return new Customer(id, name, phoneNumber, email, vehicles);
    }

    public Result<Updated> Update(string name, string email, string phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return CustomerErrors.NameRequired;
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            return CustomerErrors.EmailRequired;
        }

        if (string.IsNullOrWhiteSpace(phoneNumber) || !Regex.IsMatch(phoneNumber, @"^\+?\d{7,15}$"))
        {
            return CustomerErrors.InvalidPhoneNumber;
        }

        Name = name;
        Email = email;
        PhoneNumber = phoneNumber;

        return Result.Updated;
    }

    public Result<Updated> LinkUserAccount(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return CustomerErrors.UserIdRequired;
        }

        UserId = userId;

        return Result.Updated;
    }

    public Result<Updated> AddVehicle(Vehicle vehicle)
    {
        ArgumentNullException.ThrowIfNull(vehicle);

        if (_vehicles.Any(v => v.Vin == vehicle.Vin))
        {
            return VehicleErrors.DuplicateVin;
        }

        if (_vehicles.Any(v => v.LicensePlate.Equals(vehicle.LicensePlate, StringComparison.OrdinalIgnoreCase)))
        {
            return VehicleErrors.DuplicateLicensePlate;
        }

        _vehicles.Add(vehicle);

        return Result.Updated;
    }
}
