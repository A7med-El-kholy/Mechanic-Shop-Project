namespace MechanicShop.Domain.Invoices;

using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.WorkOrders;

/// <summary>
/// A financial document frozen at completion time: the work order's services,
/// their prices AS CHARGED, tax — and the payment story afterwards. Money
/// semantics live here (pay, choose cash, record cash), not in controllers.
///
/// Created automatically when a work order completes; the workshop-first flow
/// keeps the invoice 1:1 with its job.
/// </summary>
public sealed class Invoice
{
    public Guid Id { get; private set; }

    /// <summary>Human-facing number (INV-XXXXXXXX); unique, shown on every list.</summary>
    public string Number { get; private set; } = string.Empty;

    public Guid WorkOrderId { get; private set; }

    public WorkOrders.WorkOrder? WorkOrder { get; private set; }

    /// <summary>Business date the document was issued (the job's scheduled date) —
    /// print view needs it; NOT an audit column.</summary>
    public DateOnly IssuedOn { get; private set; }

    public decimal Subtotal { get; private set; }

    /// <summary>A plain decimal in [0,1], stored so historical totals stay stable.</summary>
    public decimal TaxRate { get; private set; }

    public InvoiceStatus Status { get; private set; }

    public PaymentMethod? PaymentMethod { get; private set; }

    public List<InvoiceLineItem> LineItems { get; private set; } = [];

    private Invoice()
    {
    }

    public decimal TaxAmount => Math.Round(Subtotal * TaxRate, 2);

    public decimal TotalAmount => Math.Round(Subtotal * (1 + TaxRate), 2);

    public static Result<Invoice> Create(
        Guid id,
        string number,
        Guid workOrderId,
        DateOnly issuedOn,
        List<(Guid ServiceTaskId, string ServiceName, decimal Amount)> lines,
        decimal taxRate)
    {
        if (id == Guid.Empty)
        {
            return InvoiceErrors.IdRequired;
        }

        if (string.IsNullOrWhiteSpace(number))
        {
            return InvoiceErrors.NumberRequired;
        }

        if (workOrderId == Guid.Empty)
        {
            return InvoiceErrors.WorkOrderIdRequired;
        }

        if (issuedOn == default)
        {
            return InvoiceErrors.IssuedOnRequired;
        }

        if (lines is null || lines.Count == 0)
        {
            return InvoiceErrors.LinesRequired;
        }

        if (taxRate < 0m || taxRate > 1m)
        {
            return InvoiceErrors.TaxRateInvalid;
        }

        var items = new List<InvoiceLineItem>();

        foreach (var line in lines)
        {
            var created = InvoiceLineItem.Create(line.ServiceTaskId, line.ServiceName, line.Amount);

            if (created.IsError)
            {
                return created.Errors;
            }

            items.Add(created.Value);
        }

        return new Invoice
        {
            Id = id,
            Number = number.Trim().ToUpperInvariant(),
            WorkOrderId = workOrderId,
            IssuedOn = issuedOn,
            Subtotal = items.Sum(li => li.Amount),
            TaxRate = taxRate,
            Status = InvoiceStatus.Unpaid,
            LineItems = items,
        };
    }

    /// <summary>Closes an invoice after confirmed payment. Customer-facing card
    /// collection is unavailable until a gateway is configured.</summary>
    public Result<Updated> Pay(PaymentMethod method)
    {
        if (Status == InvoiceStatus.Paid)
        {
            return InvoiceErrors.AlreadyPaid;
        }

        Status = InvoiceStatus.Paid;
        PaymentMethod = method;

        return Result.Updated;
    }

    /// <summary>Customer chooses to bring cash at pickup; the manager confirms later.</summary>
    public Result<Updated> SelectCashAtShop()
    {
        if (Status == InvoiceStatus.Paid)
        {
            return InvoiceErrors.AlreadyPaid;
        }

        if (Status == InvoiceStatus.CashPending)
        {
            return InvoiceErrors.AlreadyCashPending;
        }

        Status = InvoiceStatus.CashPending;

        // The property and the enum share a name — qualify the enum, not the
        // nullable property.
        PaymentMethod = global::MechanicShop.Domain.Invoices.PaymentMethod.Cash;

        return Result.Updated;
    }

    public static string NewNumber() =>
        "INV-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
}
