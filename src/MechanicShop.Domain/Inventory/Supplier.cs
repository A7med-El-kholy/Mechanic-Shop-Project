using MechanicShop.Domain.Common;
using MechanicShop.Domain.Common.Results;
using System.Net.Mail;

namespace MechanicShop.Domain.Inventory;

public sealed class Supplier : Entity
{
    public string Name { get; private set; } = string.Empty;
    public string? ContactName { get; private set; }
    public string? Email { get; private set; }
    public string? Phone { get; private set; }
    public bool IsActive { get; private set; }

    private Supplier() { }

    private Supplier(Guid id, string name, string? contactName, string? email, string? phone) : base(id)
    {
        Name = name.Trim();
        ContactName = string.IsNullOrWhiteSpace(contactName) ? null : contactName.Trim();
        Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim();
        Phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim();
        IsActive = true;
    }

    public static Result<Supplier> Create(Guid id, string name, string? contactName, string? email, string? phone)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 120) return SupplierErrors.NameInvalid;
        if (contactName?.Trim().Length > 100) return SupplierErrors.ContactInvalid;
        if (email?.Trim().Length > 150) return SupplierErrors.EmailInvalid;
        if (!string.IsNullOrWhiteSpace(email))
        {
            try
            {
                var parsed = new MailAddress(email.Trim());
                if (!string.Equals(parsed.Address, email.Trim(), StringComparison.OrdinalIgnoreCase)) return SupplierErrors.EmailInvalid;
            }
            catch (FormatException)
            {
                return SupplierErrors.EmailInvalid;
            }
        }
        if (phone?.Trim().Length > 30) return SupplierErrors.PhoneInvalid;
        return new Supplier(id, name, contactName, email, phone);
    }
}
