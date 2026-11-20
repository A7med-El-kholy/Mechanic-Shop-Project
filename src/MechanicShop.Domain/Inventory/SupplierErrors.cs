using MechanicShop.Domain.Common.Results;

namespace MechanicShop.Domain.Inventory;

public static class SupplierErrors
{
    public static Error NotFound(Guid id) => Error.NotFound("Supplier_NotFound", $"Supplier '{id}' was not found.");
    public static Error NameInvalid => Error.Validation("Supplier_NameInvalid", "Supplier name is required and cannot exceed 120 characters.");
    public static Error ContactInvalid => Error.Validation("Supplier_ContactInvalid", "Contact name cannot exceed 100 characters.");
    public static Error EmailInvalid => Error.Validation("Supplier_EmailInvalid", "Enter a valid supplier email up to 150 characters.");
    public static Error PhoneInvalid => Error.Validation("Supplier_PhoneInvalid", "Supplier phone cannot exceed 30 characters.");
    public static Error AlreadyExists(string name) => Error.Conflict("Supplier_AlreadyExists", $"Supplier '{name}' already exists.");
    public static Error Inactive => Error.Conflict("Supplier_Inactive", "This supplier is inactive and cannot receive new purchase orders.");
}
