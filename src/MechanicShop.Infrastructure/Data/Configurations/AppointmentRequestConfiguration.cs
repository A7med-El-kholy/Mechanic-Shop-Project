using MechanicShop.Domain.Appointments;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MechanicShop.Infrastructure.Data.Configurations;

public sealed class AppointmentRequestConfiguration : IEntityTypeConfiguration<AppointmentRequest>
{
    public void Configure(EntityTypeBuilder<AppointmentRequest> builder)
    {
        builder.HasKey(r => r.Id).IsClustered(false);

        builder.Property(r => r.Id)
               .ValueGeneratedNever();

        builder.Property(r => r.PreferredDate);

        builder.Property(r => r.Status);

        // The triage record: what the customer reported and what their assistant
        // session claimed about urgency. Null urgency = nobody assessed it.
        builder.Property(r => r.Notes)
               .HasMaxLength(AppointmentRequest.NotesMaxLength);

        builder.Property(r => r.Urgency);

        // A request hangs off the customer; when the customer goes (only possible
        // with a clean history), their requests go with them.
        builder.HasOne(r => r.Customer)
               .WithMany()
               .HasForeignKey(r => r.CustomerId);

        // One delete cascade here (the request dies with its car — a request for a
        // car that is gone is meaningless). The customer-side FK is deliberately
        // RESTRICT: SQL Server refuses multiple cascade paths into one table, and
        // the customer-delete rule already requires a clean profile via the app
        // guard, so vehicles are the single cascade route into requests.
        builder.HasOne(r => r.Vehicle)
               .WithMany()
               .HasForeignKey(r => r.VehicleId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.ServiceTask)
               .WithMany()
               .HasForeignKey(r => r.ServiceTaskId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(r => r.CustomerId);

        builder.HasIndex(r => r.PreferredDate);
    }
}
