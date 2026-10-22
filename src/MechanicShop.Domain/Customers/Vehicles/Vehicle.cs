using System.Text.RegularExpressions;

using MechanicShop.Domain.Common;
using MechanicShop.Domain.Common.Results;

namespace MechanicShop.Domain.Customers.Vehicles;

public sealed class Vehicle : Entity
{
    // VINs exclude I, O and Q to avoid confusion with the digits 1 and 0.
    private static readonly Regex VinPattern = new("^[A-HJ-NPR-Z0-9]{17}$", RegexOptions.Compiled);

    public Guid CustomerId { get; private set; }
    public string Make { get; private set; } = string.Empty;
    public string Model { get; private set; } = string.Empty;
    public int Year { get; private set; }
    public string LicensePlate { get; private set; } = string.Empty;
    public string Vin { get; private set; } = string.Empty;
    public bool IsArchived { get; private set; }
    public Customer? Customer { get; set; }

    public string VehicleInfo => $"{Make} {Model} {Year}";

    private Vehicle()
    { }

    private Vehicle(Guid id, string make, string model, int year, string licensePlate, string vin, Guid customerId)
        : base(id)
    {
        CustomerId = customerId;
        Make = make;
        Model = model;
        Year = year;
        LicensePlate = licensePlate;
        Vin = vin;
    }

    public static Result<Vehicle> Create(Guid id, Guid customerId, string make, string model, int year, string licensePlate, string vin)
    {
        if (customerId == Guid.Empty)
        {
            return VehicleErrors.CustomerIdRequired;
        }

        if (string.IsNullOrWhiteSpace(make))
        {
            return VehicleErrors.MakeRequired;
        }

        if (string.IsNullOrWhiteSpace(model))
        {
            return VehicleErrors.ModelRequired;
        }

        // The car was invented in 1886; the future has not happened yet.
        if (year < 1886 || year > DateTime.UtcNow.Year + 1)
        {
            return VehicleErrors.YearInvalid;
        }

        if (string.IsNullOrWhiteSpace(licensePlate))
        {
            return VehicleErrors.LicensePlateRequired;
        }

        var normalizedVin = (vin ?? string.Empty).Trim().ToUpperInvariant();

        if (!VinPattern.IsMatch(normalizedVin))
        {
            return VehicleErrors.InvalidVin;
        }

        return new Vehicle(id, make, model, year, licensePlate.ToUpperInvariant(), normalizedVin, customerId);
    }

    public Result<Updated> Update(string make, string model, int year, string licensePlate, string vin)
    {
        if (string.IsNullOrWhiteSpace(make))
        {
            return VehicleErrors.MakeRequired;
        }

        if (string.IsNullOrWhiteSpace(model))
        {
            return VehicleErrors.ModelRequired;
        }

        if (year < 1886 || year > DateTime.UtcNow.Year + 1)
        {
            return VehicleErrors.YearInvalid;
        }

        if (string.IsNullOrWhiteSpace(licensePlate))
        {
            return VehicleErrors.LicensePlateRequired;
        }

        var normalizedVin = (vin ?? string.Empty).Trim().ToUpperInvariant();

        if (!VinPattern.IsMatch(normalizedVin))
        {
            return VehicleErrors.InvalidVin;
        }

        Make = make;
        Model = model;
        Year = year;
        LicensePlate = licensePlate.ToUpperInvariant();
        Vin = normalizedVin;

        return Result.Updated;
    }

    /// <summary>
    /// The prototype's archive rule: a vehicle with service history is never hard
    /// deleted — it leaves lists and new bookings but its history stays restorable.
    /// </summary>
    public void Archive()
    {
        IsArchived = true;
    }

    public void Restore()
    {
        IsArchived = false;
    }
}
