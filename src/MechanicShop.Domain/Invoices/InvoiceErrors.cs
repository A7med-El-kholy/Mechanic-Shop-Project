namespace MechanicShop.Domain.Invoices;

using MechanicShop.Domain.Common.Results;

public static class InvoiceErrors
{
    public static Error IdRequired =>
        Error.Validation("Invoice_IdRequired", "Invoice id is required.");

    public static Error NotFound(Guid invoiceId) =>
        Error.NotFound("Invoice_NotFound", $"Invoice '{invoiceId}' was not found.");

    public static Error AlreadyPaid =>
        Error.Conflict("Invoice_AlreadyPaid", "This invoice is already paid.");

    public static Error AlreadyCashPending =>
        Error.Conflict(
            "Invoice_AlreadyCashPending",
            "Cash payment at the shop is already chosen for this invoice.");

    public static Error MethodInvalid =>
        Error.Validation("Invoice_MethodInvalid", "Payment method must be Cash (1).");

    public static Error GatewayUnavailable =>
        Error.Conflict("Invoice_GatewayUnavailable", "Card payments are unavailable until a payment gateway is configured. Choose cash at the shop.");

    public static Error NotLinkedCustomerAssigned =>
        Error.Forbidden(
            "Invoice_NotOwned",
            "This invoice is not available on your profile.");

    public static Error LineInvalid =>
        Error.Validation("Invoice_LineInvalid", "Invoice line items must carry a service name and a non-negative amount.");

    public static Error LinesRequired =>
        Error.Validation("Invoice_LinesRequired", "An invoice needs at least one line item.");

    public static Error NumberRequired =>
        Error.Validation("Invoice_NumberRequired", "Invoice number is required.");

    public static Error TaxRateInvalid =>
        Error.Validation("Invoice_TaxRateInvalid", "Tax rate must be between 0 and 1.");

    public static Error WorkOrderIdRequired =>
        Error.Validation("Invoice_WorkOrderIdRequired", "The invoiced work order is required.");

    public static Error IssuedOnRequired =>
        Error.Validation("Invoice_IssuedOnRequired", "An issue date is required.");
}
