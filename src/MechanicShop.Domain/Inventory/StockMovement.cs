namespace MechanicShop.Domain.Inventory;

/// <summary>Immutable audit record paired with every stock quantity change.</summary>
public sealed class StockMovement
{
    public Guid Id { get; private set; }
    public Guid PartId { get; private set; }
    public int Delta { get; private set; }
    public int QuantityBefore { get; private set; }
    public int QuantityAfter { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public string? ActorUserId { get; private set; }
    public DateTimeOffset RecordedAt { get; private set; }

    private StockMovement() { }

    public static StockMovement Create(Guid partId, int delta, int quantityBefore, int quantityAfter,
        string reason, string? actorUserId = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            PartId = partId,
            Delta = delta,
            QuantityBefore = quantityBefore,
            QuantityAfter = quantityAfter,
            Reason = reason.Trim(),
            ActorUserId = actorUserId,
            RecordedAt = DateTimeOffset.UtcNow,
        };
}
