namespace MechanicShop.Domain.Invoices;

/// <summary>An invoice's money state. <see cref="CashPending"/> is the
/// prototype's "Cash Pending": the customer chose to pay at the shop and the
/// workshop confirms receipt later. <see cref="Paid"/> is terminal.</summary>
public enum InvoiceStatus
{
    Unpaid = 0,
    Paid = 1,
    CashPending = 2,
}

/// <summary>How the money arrived (or will arrive). Card payments are a
/// prototype-only click — no card data ever reaches the API or storage.</summary>
public enum PaymentMethod
{
    Card = 0,
    Cash = 1,
}
