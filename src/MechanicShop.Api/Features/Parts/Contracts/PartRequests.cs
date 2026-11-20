namespace MechanicShop.Api.Features.Parts.Contracts;

public sealed record CreatePartRequest(
    string Name,
    int Category,
    decimal Price,
    int Quantity,
    int MinimumQuantity);

public sealed record AdjustPartRequest(int Delta, string Reason);
