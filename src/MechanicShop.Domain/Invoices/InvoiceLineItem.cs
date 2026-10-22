namespace MechanicShop.Domain.Invoices;

using MechanicShop.Domain.Common.Results;

/// <summary>
/// One line of an invoice: WHAT service, for HOW MUCH — frozen at issue time.
/// This is deliberately a VALUE SNAPSHOT: the ServiceTaskId is a plain column
/// with no foreign key, because a catalog price change must never reach back
/// into a document the customer was already billed on. The app-level guard on
/// service-task deletion covers live work orders; invoices stay exact forever.
/// </summary>
public sealed class InvoiceLineItem
{
    public Guid Id { get; private set; }

    public Guid InvoiceId { get; private set; }

    public Guid ServiceTaskId { get; private set; }

    public string ServiceName { get; private set; } = string.Empty;

    public decimal Amount { get; private set; }

    private InvoiceLineItem()
    {
    }

    public static Result<InvoiceLineItem> Create(Guid serviceTaskId, string serviceName, decimal amount)
    {
        if (serviceTaskId == Guid.Empty)
        {
            return InvoiceErrors.LineInvalid;
        }

        if (string.IsNullOrWhiteSpace(serviceName))
        {
            return InvoiceErrors.LineInvalid;
        }

        if (amount < 0m)
        {
            return InvoiceErrors.LineInvalid;
        }

        return new InvoiceLineItem
        {
            Id = Guid.NewGuid(),
            ServiceTaskId = serviceTaskId,
            ServiceName = serviceName.Trim(),
            Amount = amount,
        };
    }
}
