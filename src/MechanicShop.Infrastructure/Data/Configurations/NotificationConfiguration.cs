using MechanicShop.Domain.Notifications;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MechanicShop.Infrastructure.Data.Configurations;

public sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.HasKey(n => n.Id).IsClustered(false);

        builder.Property(n => n.Id)
               .ValueGeneratedNever();

        builder.Property(n => n.Title)
               .IsRequired()
               .HasMaxLength(Notification.TitleMaxLength);

        builder.Property(n => n.Message)
               .IsRequired()
               .HasMaxLength(Notification.MessageMaxLength);

        // A customer's copies are their inbox — derived event text, not history
        // documents (the facts live in WorkOrders/Invoices). Deleting a profile
        // therefore takes its copies along; the application delete guards already
        // refuse removal while any real history exists.
        builder.HasOne(n => n.Customer)
               .WithMany()
               .HasForeignKey(n => n.CustomerId)
               .OnDelete(DeleteBehavior.Cascade);

        // The deep link disappears with the work order: a scheduled job that is
        // deleted was never work, so its copies go too (the application guard
        // already refuses deleting anything past Scheduled).
        builder.HasOne(n => n.WorkOrder)
               .WithMany()
               .HasForeignKey(n => n.WorkOrderId)
               .OnDelete(DeleteBehavior.Cascade);

        // Inbox read: filter by audience/owner newest-first.
        builder.HasIndex(n => new { n.Audience, n.CustomerId, n.CreatedOn });

        // Bell badge: COUNT where unread.
        builder.HasIndex(n => new { n.Audience, n.CustomerId, n.IsRead });
    }
}
