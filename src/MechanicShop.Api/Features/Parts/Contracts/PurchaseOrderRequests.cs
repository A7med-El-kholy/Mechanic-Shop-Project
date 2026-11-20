namespace MechanicShop.Api.Features.Parts.Contracts;

public sealed record PurchaseOrderItemPayload(Guid PartId, int Quantity, decimal UnitCost);
public sealed record CreatePurchaseOrderRequest(Guid SupplierId, List<PurchaseOrderItemPayload> Items);
