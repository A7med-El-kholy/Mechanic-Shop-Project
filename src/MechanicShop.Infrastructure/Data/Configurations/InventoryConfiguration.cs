using MechanicShop.Domain.Inventory;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MechanicShop.Infrastructure.Data.Configurations;

public sealed class SupplierConfiguration : IEntityTypeConfiguration<Supplier>
{
    public void Configure(EntityTypeBuilder<Supplier> builder)
    {
        builder.HasKey(supplier => supplier.Id).IsClustered(false);
        builder.Property(supplier => supplier.Id).ValueGeneratedNever();
        builder.Property(supplier => supplier.Name).HasMaxLength(120).IsRequired();
        builder.Property(supplier => supplier.ContactName).HasMaxLength(100);
        builder.Property(supplier => supplier.Email).HasMaxLength(150);
        builder.Property(supplier => supplier.Phone).HasMaxLength(30);
        builder.HasIndex(supplier => supplier.Name).IsUnique();
    }
}

public sealed class PurchaseOrderConfiguration : IEntityTypeConfiguration<PurchaseOrder>
{
    public void Configure(EntityTypeBuilder<PurchaseOrder> builder)
    {
        builder.HasKey(order => order.Id).IsClustered(false);
        builder.Property(order => order.Id).ValueGeneratedNever();
        builder.Property(order => order.Number).HasMaxLength(20).IsRequired();
        builder.Property(order => order.OrderedOn).HasColumnType("date");
        builder.Property(order => order.ReceivedOn).HasColumnType("date");
        builder.Property(order => order.RowVersion).IsRowVersion();
        builder.HasIndex(order => order.Number).IsUnique();
        builder.HasIndex(order => order.Status);
        builder.HasOne(order => order.Supplier).WithMany().HasForeignKey(order => order.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(order => order.Lines).WithOne().HasForeignKey(line => line.PurchaseOrderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class PurchaseOrderLineConfiguration : IEntityTypeConfiguration<PurchaseOrderLine>
{
    public void Configure(EntityTypeBuilder<PurchaseOrderLine> builder)
    {
        builder.HasKey(line => line.Id).IsClustered(false);
        builder.Property(line => line.Id).ValueGeneratedNever();
        builder.Property(line => line.PartName).HasMaxLength(100).IsRequired();
        builder.Property(line => line.UnitCost).HasPrecision(10, 2);
        builder.HasOne<Part>().WithMany().HasForeignKey(line => line.PartId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(line => line.PartId);
    }
}

public sealed class StockMovementConfiguration : IEntityTypeConfiguration<StockMovement>
{
    public void Configure(EntityTypeBuilder<StockMovement> builder)
    {
        builder.HasKey(m => m.Id).IsClustered(false);
        builder.Property(m => m.Id).ValueGeneratedNever();
        builder.Property(m => m.Reason).HasMaxLength(200).IsRequired();
        builder.Property(m => m.ActorUserId).HasMaxLength(450);
        builder.HasOne<Part>().WithMany().HasForeignKey(m => m.PartId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(m => new { m.PartId, m.RecordedAt });
    }
}

public sealed class PartConfiguration : IEntityTypeConfiguration<Part>
{
    public void Configure(EntityTypeBuilder<Part> builder)
    {
        builder.HasKey(p => p.Id).IsClustered(false);

        builder.Property(p => p.Id)
               .ValueGeneratedNever();

        builder.Property(p => p.Name)
               .HasMaxLength(100);

        // The shelf's name is its rude identity in the shop and on tickets.
        builder.HasIndex(p => p.Name)
               .IsUnique();

        builder.Property(p => p.Price)
               .HasPrecision(10, 2);

        builder.Property(p => p.RowVersion)
               .IsRowVersion();

        builder.HasIndex(p => p.Category);
    }
}

public sealed class PartOrderConfiguration : IEntityTypeConfiguration<PartOrder>
{
    public void Configure(EntityTypeBuilder<PartOrder> builder)
    {
        builder.HasKey(o => o.Id).IsClustered(false);

        builder.Property(o => o.Id)
               .ValueGeneratedNever();

        builder.Property(o => o.Number)
               .HasMaxLength(16);

        builder.HasIndex(o => o.Number)
               .IsUnique();

        builder.Property(o => o.Subtotal)
               .HasPrecision(10, 2);

        builder.Property(o => o.TaxRate)
               .HasPrecision(4, 3);

        // An order belongs to its buyer forever; a buyer with orders refuses
        // deletion at the application layer, so this FK stays intact.
        builder.HasOne(o => o.Customer)
               .WithMany()
               .HasForeignKey(o => o.CustomerId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(o => o.Lines)
               .WithOne()
               .HasForeignKey(li => li.PartOrderId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(o => o.Status);
        builder.HasIndex(o => o.CustomerId);
    }
}

public sealed class PartOrderLineConfiguration : IEntityTypeConfiguration<PartOrderLine>
{
    public void Configure(EntityTypeBuilder<PartOrderLine> builder)
    {
        builder.HasKey(li => li.Id).IsClustered(false);

        builder.Property(li => li.Id)
               .ValueGeneratedNever();

        builder.Property(li => li.PartName)
               .HasMaxLength(100);

        builder.Property(li => li.UnitPrice)
               .HasPrecision(10, 2);

        // The referenced shelf: deleting a part with order history is refused
        // by an application guard before the FK ever matters.
        builder.HasOne<Part>()
               .WithMany()
               .HasForeignKey(li => li.PartId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
