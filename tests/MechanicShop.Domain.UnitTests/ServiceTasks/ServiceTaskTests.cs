using AwesomeAssertions;

using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.ServiceTasks;
using MechanicShop.Domain.ServiceTasks.Enums;

namespace MechanicShop.Domain.UnitTests.ServiceTasks;

/// <summary>
/// Catalog invariants: name required and bounded, price strictly positive with a
/// sane ceiling, duration restricted to the 15-minute calendar grid. These rules
/// are what keep F4 scheduling arithmetic honest.
/// </summary>
public sealed class ServiceTaskTests
{
    [Theory]
    [InlineData(30, 85)]
    [InlineData(45, 45)]
    [InlineData(60, 85)]
    [InlineData(120, 520)]
    [InlineData(180, 9999)]
    public void Create_WithValidInput_SetsTrimmedProperties(int minutes, decimal price)
    {
        var result = ServiceTask.Create(Guid.NewGuid(), "  Engine Oil Change  ", price, minutes);

        result.IsError.Should().BeFalse();
        result.Value.Name.Should().Be("Engine Oil Change");
        result.Value.BasePrice.Should().Be(price);
        result.Value.EstimatedDurationInMins.Should().Be((ServiceDurationInMinutes)minutes);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithBlankName_ReturnsNameRequired(string? name)
    {
        var result = ServiceTask.Create(Guid.NewGuid(), name!, 85m, 30);

        result.IsError.Should().BeTrue();
        result.TopError.Should().Be(ServiceTaskErrors.NameRequired);
    }

    [Fact]
    public void Create_With151CharacterName_ReturnsNameTooLong()
    {
        var result = ServiceTask.Create(Guid.NewGuid(), new string('a', 151), 85m, 30);

        result.IsError.Should().BeTrue();
        result.TopError.Should().Be(ServiceTaskErrors.NameTooLong);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(10000.01)]
    public void Create_WithPriceOutOfRange_ReturnsBasePriceInvalid(decimal price)
    {
        var result = ServiceTask.Create(Guid.NewGuid(), "Brake Replacement", price, 90);

        result.IsError.Should().BeTrue();
        result.TopError.Should().Be(ServiceTaskErrors.BasePriceInvalid);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(17)]
    [InlineData(37)]
    [InlineData(195)]
    public void Create_WithOffGridDuration_ReturnsDurationInvalid(int minutes)
    {
        var result = ServiceTask.Create(Guid.NewGuid(), "Tire Rotation", 45m, minutes);

        result.IsError.Should().BeTrue();
        result.TopError.Should().Be(ServiceTaskErrors.DurationInvalid);
    }

    [Fact]
    public void Update_WithValidInput_ReplacesAllFields()
    {
        var task = ServiceTask.Create(Guid.NewGuid(), "Air Conditioning Recharge", 150m, 30).Value;

        var result = task.Update("Air Conditioning Recharge Plus", 175m, 45);

        result.IsError.Should().BeFalse();
        task.Name.Should().Be("Air Conditioning Recharge Plus");
        task.BasePrice.Should().Be(175m);
        task.EstimatedDurationInMins.Should().Be(ServiceDurationInMinutes.Min45);
    }

    [Fact]
    public void Update_WithInvalidPrice_ReturnsErrorWithoutChangingEntity()
    {
        var task = ServiceTask.Create(Guid.NewGuid(), "Brake Replacement", 245m, 90).Value;

        var result = task.Update("Brake Replacement", 0m, 90);

        result.IsError.Should().BeTrue();
        task.BasePrice.Should().Be(245m);
    }
}
