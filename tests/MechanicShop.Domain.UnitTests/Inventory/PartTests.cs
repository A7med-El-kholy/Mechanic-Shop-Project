using AwesomeAssertions;
using MechanicShop.Domain.Inventory;

namespace MechanicShop.Domain.UnitTests.Inventory;

public sealed class PartTests
{
    [Fact]
    public void Adjust_WhenRemovalExceedsStock_ReturnsErrorAndPreservesQuantity()
    {
        var part = Part.Create(Guid.NewGuid(), "Oil filter", (int)PartCategory.Engine, 12m, 3, 1).Value!;

        var result = part.Adjust(-4);

        result.IsError.Should().BeTrue();
        part.Quantity.Should().Be(3);
    }

    [Fact]
    public void Adjust_WhenAdditionOverflowsQuantity_ReturnsErrorAndPreservesQuantity()
    {
        var part = Part.Create(Guid.NewGuid(), "Oil filter", (int)PartCategory.Engine, 12m, int.MaxValue, 1).Value!;

        var result = part.Adjust(1);

        result.IsError.Should().BeTrue();
        part.Quantity.Should().Be(int.MaxValue);
    }
}
