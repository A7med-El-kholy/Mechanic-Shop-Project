using System.Text;

using FluentValidation;

using MechanicShop.Api.Common;
using MechanicShop.Api.Features.Auth;
using MechanicShop.Api.Features.Customers;
using MechanicShop.Api.Features.Users;
using MechanicShop.Api.Features.ServiceTasks;
using MechanicShop.Api.Features.Vehicles;
using MechanicShop.Application.Common.Behaviors;
using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Infrastructure.Data;
using MechanicShop.Infrastructure.Identity;

using MediatR;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// --- Database: SQL Server via the connection string "ConnectionStrings:MechanicShop".
// The value is supplied by user-secrets locally and by environment variables in deployment;
// it is never committed to source control.
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("MechanicShop")));

// --- ASP.NET Core Identity: password policy, lockout, and the AppUser/role stores.
builder.Services
    .AddIdentity<AppUser, IdentityRole>(options =>
    {
        options.Password.RequiredLength = 8;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = true;

        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

// --- JWT bearer authentication.
// MapInboundClaims=false keeps the "sub" claim as-is instead of rewriting it to the
// long ClaimTypes.NameIdentifier URI, which is what AuthEndpoints' /me lookup expects.
var jwtSettings = builder.Configuration.GetSection("JwtSettings");

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtSettings["Issuer"],
            ValidateAudience = true,
            ValidAudience = jwtSettings["Audience"],
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtSettings["Secret"] ?? string.Empty)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(Policies.IsManager, policy => policy.RequireRole(AppRoles.Manager));
    options.AddPolicy(Policies.IsTechnician, policy => policy.RequireRole(AppRoles.Technician));
    options.AddPolicy(Policies.IsCustomer, policy => policy.RequireRole(AppRoles.Customer));
    options.AddPolicy(Policies.IsStaff, policy => policy.RequireRole(AppRoles.Manager, AppRoles.Technician));
});

// IHttpContextAccessor lets ApplicationUser reach the request's claims anywhere in
// the container; ApplicationUser adapts those claims to the Application-side IUser.
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IUser, CurrentUser>();

// --- MediatR: commands/queries in Application are dispatched as messages, with a
// validation pipeline behavior short-circuiting invalid requests before a handler runs.
builder.Services.AddMediatR(configuration =>
{
    configuration.RegisterServicesFromAssembly(typeof(IIdentityService).Assembly);
    configuration.AddOpenBehavior(typeof(ValidationBehavior<,>));
});

// FluentValidation validators are scanned from the Application assembly; the
// ValidationBehavior above is handed whichever of them matches the incoming request.
builder.Services.AddValidatorsFromAssembly(typeof(IIdentityService).Assembly);

// --- Application services.
// IAppDbContext exposes the DbContext to handlers without leaking Infrastructure types;
// resolving the SAME scoped AppDbContext instance keeps EF tracking coherent.
builder.Services.AddScoped<IAppDbContext>(services => services.GetRequiredService<AppDbContext>());
builder.Services.AddScoped<ITokenProvider, TokenProvider>();
builder.Services.AddScoped<IIdentityService, IdentityService>();

var app = builder.Build();

// Seed roles and the optional bootstrap manager before serving requests.
// Idempotent: only inserts what is not already present, and migrates first.
await SeedData.SeedAsync(app.Services);

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => "Hello World!");
app.MapAuthEndpoints();
app.MapUsersEndpoints();
app.MapCustomersEndpoints();
app.MapVehiclesEndpoints();
app.MapServiceTasksEndpoints();

app.Run();

// top level statement approach create Program class internal and main method inside it
// so, in integration test we can not access Program class because it is will be internal
// so we do this
// Makes the generated entry point accessible to the integration test assembly.
public partial class Program;
