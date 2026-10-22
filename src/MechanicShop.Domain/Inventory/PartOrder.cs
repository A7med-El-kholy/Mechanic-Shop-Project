namespace MechanicShop.Domain.Inventory;

using MechanicShop.Domain.Common.Results;

/// <summary>One purchased line. Part is referenced live, but WHAT WAS CHARGED
/// (name, unit price) is a snapshot on the line — the same financial-document
/// reasoning as invoice lines.</summary>
public sealed class PartOrderLine
{
    public Guid Id { get; private set; }

    public Guid PartOrderId { get; private set; }

    public Guid PartId { get; private set; }

    public string PartName { get; private set; } = string.Empty;

    public decimal UnitPrice { get; private set; }

    public int Quantity { get; private set; }

    private PartOrderLine()
    {
    }

    public static Result<PartOrderLine> Create(Guid partId, string partName, decimal unitPrice, int quantity)
    {
        if (partId == Guid.Empty)
        {
            return PartOrderErrors.NotFound(partId);
        }

        if (string.IsNullOrWhiteSpace(partName))
        {
            return PartOrderErrors.ItemsRequired;
        }

        if (unitPrice < 0)
        {
            return PartOrderErrors.QuantityInvalid;
        }

        if (quantity < 1)
        {
            return PartOrderErrors.QuantityInvalid;
        }

        return new PartOrderLine
        {
            Id = Guid.NewGuid(),
            PartId = partId,
            PartName = partName.Trim(),
            UnitPrice = unitPrice,
            Quantity = quantity,
        };
    }
}

/// <summary>The customer's parts receipt. Card orders arrive Paid; cash
/// orders arrive CashPending and the manager closes them when the money is
/// counted. Stock is already deducted at placement — reservations are real.</summary>
public sealed class PartOrder
{
    public Guid Id { get; private set; }

    /// <summary>Human-facing number (ORD-XXXXXXXX); unique, shown on every list.</summary>
    public string Number { get; private set; } = string.Empty;

    public Guid CustomerId { get; private set; }

    public Customers.Customer? Customer { get; private set; }

    public DateOnly PlacedOn { get; private set; }

    public decimal Subtotal { get; private set; }

    /// <summary>Stored on the document so history stays stable when the shop
    /// tax changes — same reasoning as invoices.</summary>
    public decimal TaxRate { get; private set; }

    public PartOrderStatus Status { get; private set; }

    public Invoices.PaymentMethod? PaymentMethod { get; private set; }

    public List<PartOrderLine> Lines { get; private set; } = [];

    private PartOrder()
    {
    }

    public decimal TaxAmount => Math.Round(Subtotal * TaxRate, 2);

    public decimal TotalAmount => Math.Round(Subtotal * (1 + TaxRate), 2);

    public static string NewNumber() =>
        "ORD-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

    /// <summary>Builds the order from the validated cart. Payment decides the
    /// starting state: card = Paid now, cash = reserved for pickup.</summary>
    public static Result<PartOrder> Place(
        Guid id,
        string number,
        Guid customerId,
        DateOnly placedOn,
        List<PartOrderLine> lines,
        decimal taxRate,
        Invoices.PaymentMethod method)
    {
        if (id == Guid.Empty)
        {
            return PartOrderErrors.NotFound(id);
        }

        if (string.IsNullOrWhiteSpace(number))
        {
            return PartOrderErrors.ItemsRequired;
        }

        if (lines is null || lines.Count == 0)
        {
            return PartOrderErrors.ItemsRequired;
        }

        if (taxRate < 0 || taxRate > 1)
        {
            return PartOrderErrors.MethodInvalid;
        }

        return new PartOrder
        {
            Id = id,
            Number = number.ToUpperInvariant(),
            CustomerId = customerId,
            PlacedOn = placedOn,
            Subtotal = lines.Sum(li => li.UnitPrice * li.Quantity),
            TaxRate = taxRate,
            Status = method == Invoices.PaymentMethod.Card
                ? PartOrderStatus.Paid
                : PartOrderStatus.CashPending,
            PaymentMethod = method,
            Lines = lines,
        };
    }

    /// <summary>Money physically arrived at the counter.</summary>
    public Result<Updated> RecordCashReceived()
    {
        if (Status == PartOrderStatus.Paid)
        {
            return PartOrderErrors.AlreadyPaid;
        }

        Status = PartOrderStatus.Paid;
        PaymentMethod = Invoices.PaymentMethod.Cash;

        return Result.Updated;
    }
}
