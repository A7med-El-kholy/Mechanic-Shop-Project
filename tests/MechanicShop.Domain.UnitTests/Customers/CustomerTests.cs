using AwesomeAssertions;

using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.Customers;
using MechanicShop.Domain.Customers.Vehicles;

namespace MechanicShop.Domain.UnitTests.Customers;

/// <summary>
/// Customer entity invariants run BEFORE any database request is made — the entity is
/// the last line of defence for validation. These tests pin that behaviour.
/// </summary>
public sealed class CustomerTests
{
    private static readonly List<Vehicle> OneVehicle =
    [
        Vehicle.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Toyota",
            "Corolla",
            2024,
            "ABC-1234",
            "1HGFC2F59JA104822").Value! // valid: 17 chars, no I/O/Q
    ];

    private const string ValidName = "Sarah Peter";
    private const string ValidPhone = "+201234567890"; // optional '+' + 12 digits
    private const string ValidEmail = "sarah@example.com";

    [Fact]
    public void Create_WithValidInput_BuildsCustomer()
    {
        var result = Customer.Create(Guid.NewGuid(), ValidName, ValidPhone, ValidEmail, OneVehicle);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Name.Should().Be(ValidName);
        result.Value!.Email.Should().Be(ValidEmail);
        result.Value!.Vehicles.Should().ContainSingle();
    }

    [Fact]
    public void Create_WithBlankName_ReturnsNameRequired()
    {
        var result = Customer.Create(Guid.NewGuid(), "   ", ValidPhone, ValidEmail, OneVehicle);

        result.IsError.Should().BeTrue();
        result.TopError.Type.Should().Be(ErrorKind.Validation);
        result.TopError.Code.Should().Be("Customer_Name_Required");
    }

    [Theory]
    [InlineData("0123")]           // too short
    [InlineData("abcdefghij")]     // letters
    [InlineData("12345678901234567")] // over 15 digits
    public void Create_WithInvalidPhone_ReturnsPhoneNumberInvalid(string phone)
    {
        var result = Customer.Create(Guid.NewGuid(), ValidName, phone, ValidEmail, OneVehicle);

        result.IsError.Should().BeTrue();
        result.TopError.Code.Should().Be("Customer_PhoneNumber_Invalid");
    }

    [Fact]
    public void Create_WithInvalidEmail_ReturnsEmailInvalid()
    {
        var result = Customer.Create(Guid.NewGuid(), ValidName, ValidPhone, "not-an-email", OneVehicle);

        result.IsError.Should().BeTrue();
        result.TopError.Code.Should().Be("Customer_Email_Invalid");
    }

    [Fact]
    public void Update_WithValidInput_ChangesFields()
    {
        var customer = Customer.Create(Guid.NewGuid(), ValidName, ValidPhone, ValidEmail, OneVehicle).Value!;
        var originalId = customer.Id;

        var updateResult = customer.Update("New Name", "new@example.com", "+201122334455");

        updateResult.IsSuccess.Should().BeTrue();
        customer.Id.Should().Be(originalId);
        customer.Name.Should().Be("New Name");
        customer.Email.Should().Be("new@example.com");
    }

    [Fact]
    public void AddVehicle_WithDuplicateVin_ReturnsDuplicateVin()
    {
        var customer = Customer.Create(Guid.NewGuid(), ValidName, ValidPhone, ValidEmail, OneVehicle).Value!;

        var twin = Vehicle.Create(
            Guid.NewGuid(),
            customer.Id,
            "Honda",
            "Civic",
            2023,
            "ZZZ-9999", // different plate, SAME VIN
            "1HGFC2F59JA104822").Value!;

        var addResult = customer.AddVehicle(twin);

        addResult.IsError.Should().BeTrue();
        addResult.TopError.Type.Should().Be(ErrorKind.Conflict);
    }

    [Fact]
    public void AddVehicle_WithNewVehicle_Appends()
    {
        var customer = Customer.Create(Guid.NewGuid(), ValidName, ValidPhone, ValidEmail, OneVehicle).Value!;

        var second = Vehicle.Create(
            Guid.NewGuid(),
            customer.Id,
            "Honda",
            "Civic",
            2023,
            "ABC-4321",
            "1FA6P0849E5100001").Value!; // another valid VIN

        var addResult = customer.AddVehicle(second);

        addResult.IsSuccess.Should().BeTrue();
        customer.Vehicles.Should().HaveCount(2);
    }
}
