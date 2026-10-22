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
    }
}
