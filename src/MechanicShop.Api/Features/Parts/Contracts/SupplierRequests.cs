namespace MechanicShop.Api.Features.Parts.Contracts;

public sealed record CreateSupplierRequest(string Name, string? ContactName, string? Email, string? Phone);
