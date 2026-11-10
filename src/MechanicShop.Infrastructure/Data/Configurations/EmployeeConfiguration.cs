using MechanicShop.Domain.Employees;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MechanicShop.Infrastructure.Data.Configurations;

public sealed class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
    public void Configure(EntityTypeBuilder<Employee> builder)
    {
        builder.HasKey(e => e.Id).IsClustered(false);

        builder.Property(e => e.Id)
               .ValueGeneratedNever();

        builder.Property(e => e.FullName)
               .HasMaxLength(100)
               .IsRequired();

        // Scheduling's technician dropdown rides on this lookup frequently.
        builder.HasIndex(e => e.FullName)
               .IsUnique();

        builder.Property(e => e.Role)
               .IsRequired();

        // The staff-login link mirrors Customer.UserId: 450 chars (the Identity id
        // length), unique among linked neighbours so one account answers for one
        // roster row, NULL rows excluded from the index.
        builder.Property(e => e.UserId)
               .HasMaxLength(450)
               .IsRequired(false);

        builder.HasIndex(e => e.UserId)
               .IsUnique()
               .HasFilter("[UserId] IS NOT NULL");
    }
}
