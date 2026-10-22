using MechanicShop.Application.Common.Errors;
using MechanicShop.Domain.Customers.Vehicles;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MechanicShop.Infrastructure.Data.Configurations;

public sealed class VehicleConfiguration : IEntityTypeConfiguration<Vehicle>
{
    public void Configure(EntityTypeBuilder<Vehicle> builder)
    {
        builder.HasKey(v => v.Id).IsClustered(false);

        builder.Property(v => v.Id)
               .ValueGeneratedNever();

        builder.Property(v => v.Make)
               .IsRequired()
               .HasMaxLength(100);

        builder.Property(v => v.Model)
               .IsRequired()
               .HasMaxLength(100);

        builder.Property(v => v.Year)
               .IsRequired();

        builder.Property(v => v.LicensePlate)
               .IsRequired()
               .HasMaxLength(20);

        // Store the normalized 17-character form; shorter input would violate
        // the check the API validators run before this table is touched.
        builder.Property(v => v.Vin)
               .IsRequired()
               .HasMaxLength(17);

        // The database is the referee when two concurrent inserts race past the
        // application-level duplicate check; IX_Vehicles_Vin is also matched by name
        // in DatabaseErrors.IsVehicleVinUniqueViolation, so renaming it breaks that.
        builder.HasIndex(v => v.Vin)
               .HasDatabaseName(DatabaseErrors.VehicleVinIndexName)
               .IsUnique();

        // The prototype rejects duplicate plates at the form level ("This plate is
        // already registered."); the index carries that rule into the database.
        // Plates are stored uppercased by the entity, so the index is case-stable.
        builder.HasIndex(v => v.LicensePlate)
               .HasDatabaseName(DatabaseErrors.VehicleLicensePlateIndexName)
               .IsUnique();

        builder.HasOne(v => v.Customer)
               .WithMany(c => c.Vehicles)
               .HasForeignKey(v => v.CustomerId);
    }
}
