using AwesomeAssertions;

using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.Customers;
using MechanicShop.Domain.Customers.Vehicles;

namespace MechanicShop.Domain.UnitTests.Customers;

/// <summary>
/// VIN format and normalization rules: 17 characters, letters/digits, no I, O, Q.
/// The database index enforces uniqueness; the entity enforces shape.
/// </summary>
public sealed class VehicleTests
{
    private const string ValidVin = "1HGFC2F59JA104822";

    private static Guid SomeCustomerId() => Guid.NewGuid();

    [Fact]
    public void Create_WithValidInput_NormalizesPlateAndVinToUpper()
    {
        // 17 chars, no I/O/Q; fed in lowercase to prove normalization.
        var lowercaseVin = "1hgfc2f59ja104822";

        var result = Vehicle.Create(
            Guid.NewGuid(),
            SomeCustomerId(),
            "Toyota",
            "Corolla",
            2024,
            "abc-1234",
            lowercaseVin);

        result.IsSuccess.Should().BeTrue();
        result.Value!.LicensePlate.Should().Be("ABC-1234");
        result.Value!.Vin.Should().Be("1HGFC2F59JA104822");
    }

    [Fact]
    public void Create_WithVinContainingO_ReturnsInvalidVin()
    {
        var result = Vehicle.Create(
            Guid.NewGuid(),
            SomeCustomerId(),
            "Toyota",
            "Corolla",
            2024,
            "ABC-1234",
            "1HGFC2F59JA1O4822"); // 17 chars, contains 'O'

        result.IsError.Should().BeTrue();
        result.TopError.Code.Should().Be("Vehicle_Vin_Invalid");
    }

    [Fact]
    public void Create_WithVinContainingLowerQ_RejectsBecauseQIsBanned()
    {
        var result = Vehicle.Create(
            Guid.NewGuid(),
            SomeCustomerId(),
            "Toyota",
            "Corolla",
            2024,
            "ABC-1234",
            "1HGFC2F59JA1Q4822");

        result.IsError.Should().BeTrue();
    }

    [Fact]
    public void Create_WithShortVin_ReturnsInvalidVin()
    {
        var result = Vehicle.Create(
            Guid.NewGuid(),
            SomeCustomerId(),
            "Toyota",
            "Corolla",
            2024,
            "ABC-1234",
            "SHORT");

        result.IsError.Should().BeTrue();
        result.TopError.Code.Should().Be("Vehicle_Vin_Invalid");
    }

    [Theory]
    [InlineData(1885, "Vehicle_Year_Invalid")]
    [InlineData(2200, "Vehicle_Year_Invalid")]
    public void Create_WithOutOfRangeYear_ReturnsYearInvalid(int year, string expectedCode)
    {
        var result = Vehicle.Create(
            Guid.NewGuid(),
            SomeCustomerId(),
            "Toyota",
            "Corolla",
            year,
            "ABC-1234",
            ValidVin);

        result.IsError.Should().BeTrue();
        result.TopError.Code.Should().Be(expectedCode);
    }

    [Fact]
    public void Create_WithEmptyCustomerId_ReturnsCustomerIdRequired()
    {
        var result = Vehicle.Create(
            Guid.NewGuid(),
            Guid.Empty,
            "Toyota",
            "Corolla",
            2024,
            "ABC-1234",
            ValidVin);

        result.IsError.Should().BeTrue();
        result.TopError.Code.Should().Be("Vehicle_CustomerId_Required");
    }

    [Fact]
    public void Update_WithValidInput_ReplacesFields()
    {
        var vehicle = Vehicle.Create(
            Guid.NewGuid(),
            SomeCustomerId(),
            "Toyota",
            "Corolla",
            2024,
            "ABC-1234",
            ValidVin).Value!;

        var updateResult = vehicle.Update("Honda", "Civic", 2023, "XYZ-9876", "1FTFW1ET5DFC02893");

        updateResult.IsSuccess.Should().BeTrue();
        vehicle.Make.Should().Be("Honda");
        vehicle.VehicleInfo.Should().Be("Honda Civic 2023");
    }
}
