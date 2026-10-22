using MechanicShop.Domain.Appointments;
using MechanicShop.Domain.Customers;
using MechanicShop.Domain.Customers.Vehicles;
using MechanicShop.Domain.Employees;
using MechanicShop.Domain.Invoices;
using MechanicShop.Domain.ServiceTasks;
using MechanicShop.Domain.WorkOrders;

using Microsoft.EntityFrameworkCore;

namespace MechanicShop.Application.Common.Interfaces;

/// <summary>
/// Application-facing contract over the persistence layer. Handlers depend on THIS,
/// not on Infrastructure's AppDbContext, so the dependency still points inward:
/// Application defines what it needs, Infrastructure implements it.
/// Grows with each feature — only entities that exist today are listed.
/// </summary>
public interface IAppDbContext
{
    DbSet<Customer> Customers { get; }

    DbSet<Vehicle> Vehicles { get; }

    DbSet<ServiceTask> ServiceTasks { get; }

    DbSet<WorkOrder> WorkOrders { get; }

    DbSet<Employee> Employees { get; }

    DbSet<AppointmentRequest> AppointmentRequests { get; }

    DbSet<Invoice> Invoices { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
