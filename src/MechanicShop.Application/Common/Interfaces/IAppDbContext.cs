using MechanicShop.Domain.Customers;
using MechanicShop.Domain.Customers.Vehicles;

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

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
