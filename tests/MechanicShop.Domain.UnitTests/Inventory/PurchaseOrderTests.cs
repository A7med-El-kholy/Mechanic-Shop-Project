using AwesomeAssertions;
using MechanicShop.Domain.Inventory;

namespace MechanicShop.Domain.UnitTests.Inventory;

public sealed class PurchaseOrderTests
{
    [Fact]
    public void MarkReceived_WhenAlreadyReceived_ReturnsError()
    {
        var line = PurchaseOrderLine.Create(Guid.NewGuid(), "Brake pads", 4, 25m).Value!;
        var order = PurchaseOrder.Create(Guid.NewGuid(), "PO-TEST-0001", Guid.NewGuid(), DateOnly.FromDateTime(DateTime.UtcNow), [line]).Value!;

        order.MarkReceived(DateOnly.FromDateTime(DateTime.UtcNow)).IsSuccess.Should().BeTrue();
        order.MarkReceived(DateOnly.FromDateTime(DateTime.UtcNow)).IsError.Should().BeTrue();
        order.Status.Should().Be(PurchaseOrderStatus.Received);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(100001)]
    public void CreateLine_WhenQuantityIsOutsideBounds_ReturnsError(int quantity)
    {
        PurchaseOrderLine.Create(Guid.NewGuid(), "Brake pads", quantity, 25m).IsError.Should().BeTrue();
    }
}
