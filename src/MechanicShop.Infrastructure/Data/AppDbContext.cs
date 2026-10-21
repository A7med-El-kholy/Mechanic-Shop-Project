using MechanicShop.Infrastructure.Identity;

using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace MechanicShop.Infrastructure.Data;

/// <summary>
/// The single DbContext for the app. Inheriting IdentityDbContext gives us the
/// AspNetUsers / AspNetRoles / AspNetUserRoles tables out of the box; we'll add our
/// own domain entities (vehicles, work orders, …) here as the features land.
/// </summary>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<AppUser>(options)
{
}
