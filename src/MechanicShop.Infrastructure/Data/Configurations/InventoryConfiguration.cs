using MechanicShop.Domain.Inventory;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MechanicShop.Infrastructure.Data.Configurations;

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
