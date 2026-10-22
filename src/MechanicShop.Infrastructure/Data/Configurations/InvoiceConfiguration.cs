using MechanicShop.Domain.Invoices;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MechanicShop.Infrastructure.Data.Configurations;

public sealed class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        builder.HasKey(i => i.Id).IsClustered(false);

        builder.Property(i => i.Id)
               .ValueGeneratedNever();

        builder.Property(i => i.Number)
               .HasMaxLength(16);

        // The invoice's human number is its rude unique identity on lists.
        builder.HasIndex(i => i.Number)
               .IsUnique();

        // Money with two decimals, tax rate with room for 0.150.
        builder.Property(i => i.Subtotal)
               .HasPrecision(10, 2);

        builder.Property(i => i.TaxRate)
               .HasPrecision(4, 3);

        // One route of deletion: the invoice hangs off its job forever. Cars,
        // customers and catalog rows already refuse deletion behind history
        // guards; the work order itself has no deletion endpoint at all.
        builder.HasOne(i => i.WorkOrder)
               .WithMany()
               .HasForeignKey(i => i.WorkOrderId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(i => i.LineItems)
               .WithOne()
               .HasForeignKey(li => li.InvoiceId)
               .OnDelete(DeleteBehavior.Cascade);

        // Snapshot-only reference: storing the id, NOT creating a foreign key —
        // a billed document must not change when the catalog changes.
        builder.HasIndex(i => i.WorkOrderId)
               .IsUnique();

        builder.HasIndex(i => i.Status);

        builder.HasIndex(i => i.IssuedOn);
    }
}

public sealed class InvoiceLineItemConfiguration : IEntityTypeConfiguration<InvoiceLineItem>
{
    public void Configure(EntityTypeBuilder<InvoiceLineItem> builder)
    {
        builder.HasKey(li => li.Id).IsClustered(false);

        builder.Property(li => li.Id)
               .ValueGeneratedNever();

        builder.Property(li => li.ServiceName)
               .HasMaxLength(150);

        builder.Property(li => li.Amount)
               .HasPrecision(10, 2);

        // ServiceTaskId stands alone as data (no navigation): see Invoice
        // LineItem doc — billed lines never re-point at a moving catalog.
    }
}
