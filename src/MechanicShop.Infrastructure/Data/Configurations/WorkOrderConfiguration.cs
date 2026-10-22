using MechanicShop.Domain.ServiceTasks;
using MechanicShop.Domain.WorkOrders;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MechanicShop.Infrastructure.Data.Configurations;

public sealed class WorkOrderConfiguration : IEntityTypeConfiguration<WorkOrder>
{
    public void Configure(EntityTypeBuilder<WorkOrder> builder)
    {
        builder.HasKey(w => w.Id).IsClustered(false);

        builder.Property(w => w.Id)
               .ValueGeneratedNever();

        builder.Property(w => w.VehicleId)
               .IsRequired();

        builder.HasOne(w => w.Vehicle)
               .WithMany()
               .HasForeignKey(w => w.VehicleId);

        // A work order references catalog services without owning them — deleting a
        // catalog item can never cascade into signed-off jobs.
        builder.HasMany(w => w.ServiceTasks)
               .WithMany()
               .UsingEntity(
                   "WorkOrderServiceTask",
                   left => left.HasOne(typeof(ServiceTask)).WithMany().HasForeignKey("ServiceTaskId").OnDelete(DeleteBehavior.Restrict),
                   right => right.HasOne(typeof(WorkOrder)).WithMany().HasForeignKey("WorkOrderId").OnDelete(DeleteBehavior.Cascade),
                   join => join.HasKey("WorkOrderId", "ServiceTaskId"));

        // The vehicle-removal guard and the staff "active jobs" filter both query by
        // state; an index makes those selective scans cheap.
        builder.HasIndex(w => w.State);

        // Vehicles are always read with their work orders for guards and history.
        builder.HasIndex(w => w.VehicleId);
    }
}
