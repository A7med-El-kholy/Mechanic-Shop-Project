using System.Text;

using MechanicShop.Api.Features.Auth;
using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Infrastructure.Data;
using MechanicShop.Infrastructure.Identity;

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

builder.Services.AddAuthorization();

// --- Application services.
builder.Services.AddScoped<ITokenProvider, TokenProvider>();
builder.Services.AddScoped<IIdentityService, IdentityService>();

var app = builder.Build();

// Seed the three shop roles (Manager / Technician / Customer) before serving requests.
// Idempotent: only inserts roles that are not already present.
await SeedData.EnsureRolesAsync(app.Services);

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => "Hello World!");
app.MapAuthEndpoints();

app.Run();

// top level statement approach create Program class internal and main method inside it
// so, in integration test we can not access Program class because it is will be internal
// so we do this
// Makes the generated entry point accessible to the integration test assembly.
public partial class Program;
