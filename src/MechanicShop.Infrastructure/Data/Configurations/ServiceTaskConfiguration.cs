using MechanicShop.Application.Common.Errors;
using MechanicShop.Domain.ServiceTasks;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MechanicShop.Infrastructure.Data.Configurations;

public sealed class ServiceTaskConfiguration : IEntityTypeConfiguration<ServiceTask>
{
    public void Configure(EntityTypeBuilder<ServiceTask> builder)
    {
        builder.HasKey(t => t.Id).IsClustered(false);

        builder.Property(t => t.Id)
               .ValueGeneratedNever();

        builder.Property(t => t.Name)
               .IsRequired()
               .HasMaxLength(150);

        // Money columns get an explicit precision so EF doesn't fall back to
        // provider defaults that truncate values silently.
        builder.Property(t => t.BasePrice)
               .HasPrecision(10, 2);

        // Supported when creating tasks by hand: managers cannot save arbitrary
        // minutes, only 15-minute steps (30, 60, 90, ...) defined by the enum.
        builder.Property(t => t.EstimatedDurationInMins)
               .HasConversion<int>();

        // The API rejects duplicate names with a friendly 409 (checked by the handler);
        // this unique index is the referee when two concurrent staff inserts race.
        // IX_ServiceTasks_Name is also matched by name in
        // DatabaseErrors.IsServiceTaskNameUniqueViolation, so renaming it breaks that.
        builder.HasIndex(t => t.Name)
               .HasDatabaseName(DatabaseErrors.ServiceTaskNameIndexName)
               .IsUnique();

        // The consumed part: Restrict so a shelf with citing tasks can never
        // silently vanish (the same refuse-deletion family as work orders).
        builder.HasOne(t => t.Part)
               .WithMany()
               .HasForeignKey(t => t.PartId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
