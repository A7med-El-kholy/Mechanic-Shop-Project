namespace MechanicShop.Domain.Inventory;

using MechanicShop.Domain.Common.Results;

public static class PartErrors
{
    public static Error NotFound(Guid partId) =>
        Error.NotFound("Part_NotFound", $"Part '{partId}' was not found.");

    public static Error AlreadyExists(string name) =>
        Error.Conflict("Part_AlreadyExists", $"A part named '{name}' already exists.");

    public static Error NameRequired =>
        Error.Validation("Part_NameRequired", "Part name is required.");

    public static Error NameTooLong =>
        Error.Validation("Part_NameTooLong", "Part name cannot exceed 100 characters.");

    public static Error PriceInvalid =>
        Error.Validation("Part_PriceInvalid", "Part price must be positive and under 10,000.");

    public static Error QuantityInvalid =>
        Error.Validation("Part_QuantityInvalid", "Part quantity cannot be negative.");

    public static Error MinimumInvalid =>
        Error.Validation("Part_MinimumInvalid", "Part minimum quantity cannot be negative.");

    public static Error CategoryInvalid =>
        Error.Validation("Part_CategoryInvalid", "Part category must be Engine (0), Brakes (1), Electrical (2) or Fluids (3).");

    public static Error DeltaInvalid =>
        Error.Validation("Part_DeltaInvalid", "Stock adjustment must be a non-zero delta.");

    public static Error CitedByTasks =>
        Error.Conflict("Part_CitedByTasks", "This part is referenced by service tasks and cannot be removed.");
}
