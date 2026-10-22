namespace MechanicShop.Application.Features.Invoices;

/// <summary>The one and only place the shop's sales tax lives. The prototype
/// bills "Sales tax (15%)" on every invoice; when the shop's jurisdiction
/// changes, this default changes and HISTORICAL invoices keep their stored
/// rate — a reason tax must live on the document, not be re-derived at read.</summary>
public static class InvoiceTaxDefaults
{
    public const decimal Rate = 0.15m;
}
