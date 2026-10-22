using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Domain.Customers;
using MechanicShop.Domain.Customers.Vehicles;
using MechanicShop.Domain.ServiceTasks;
using MechanicShop.Infrastructure.Identity;

using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace MechanicShop.Infrastructure.Data;

/// <summary>
/// The single DbContext for the app. Inheriting IdentityDbContext gives us the
/// AspNetUsers / AspNetRoles / AspNetUserRoles tables out of the box. Domain entity
/// sets are registered through IAppDbContext so Application handlers stay decoupled.
/// Configurations are discovered by assembly scan, so a new entity needs no edits here.
/// </summary>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<AppUser>(options), IAppDbContext
{
    public DbSet<Customer> Customers { get; set; }
    public DbSet<Vehicle> Vehicles { get; set; }
    public DbSet<ServiceTask> ServiceTasks { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Scan this assembly for IEntityTypeConfiguration&lt;T&gt; implementations.
        // EF Core throwing "IEntityTypeConfiguration does not have a type expected" is
        // a red herring for alternatives like reflection-based discovery — this is the
        // supported approach.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
