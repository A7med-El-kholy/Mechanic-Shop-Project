using System.Text;
using System.Threading.RateLimiting;

using FluentValidation;

using MechanicShop.Api.Common;
using MechanicShop.Api.Features.Appointments;
using MechanicShop.Api.Features.Auth;
using MechanicShop.Api.Features.Customers;
using MechanicShop.Api.Features.Employees;
using MechanicShop.Api.Features.Invoices;
using MechanicShop.Api.Features.Parts;
using MechanicShop.Api.Features.Portal;
using MechanicShop.Api.Features.Notifications;
using MechanicShop.Api.Features.Reports;
using MechanicShop.Api.Features.ShopOrders;
using MechanicShop.Api.Features.Users;
using MechanicShop.Api.Features.ServiceTasks;
using MechanicShop.Api.Features.Vehicles;
using MechanicShop.Api.Features.WorkOrders;
using MechanicShop.Api.Features.AI;
using MechanicShop.Application.Features.AI;
using MechanicShop.Infrastructure.AI;
using MechanicShop.Application.Common.Behaviors;
using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Infrastructure.Data;
using MechanicShop.Infrastructure.Identity;

using MediatR;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

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

var googleClientId = builder.Configuration["GoogleAuth:ClientId"];
var googleClientSecret = builder.Configuration["GoogleAuth:ClientSecret"];

var authBuilder = builder.Services
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
    })
    // --- External-login cookie: short-lived cookie used ONLY while completing a
    // third-party sign-in (Google). It is NOT the session for regular users — that
    // is the JWT above. Kept separate so the regular auth surface stays cookie-free.
    .AddCookie("External", options =>
    {
        options.Cookie.Name = "MechanicShop.External";
        options.Cookie.Path = "/api"; // visible at every external-login endpoint
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;
        options.ExpireTimeSpan = TimeSpan.FromMinutes(10);
        options.SlidingExpiration = false;
    });

// --- Google OAuth2 — ONLY registered when both credentials are configured.
// ASP.NET Core's OAuthOptions.Validate() throws on an empty ClientId when the
// Google handler is instantiated, so an unconfigured scheme would 500 every
// request. Instead, /api/auth/google reports a clear "not configured" error.
if (!string.IsNullOrWhiteSpace(googleClientId) && !string.IsNullOrWhiteSpace(googleClientSecret))
{
    authBuilder.AddGoogle(options =>
    {
        options.SignInScheme = "External";
        options.ClientId = googleClientId;
        options.ClientSecret = googleClientSecret;
        options.CallbackPath = "/api/auth/signin-google";
        options.SaveTokens = true;
        options.Events.OnRemoteFailure = context =>
        {
            context.Response.Redirect(
                $"/api/auth/google-failure?error={Uri.EscapeDataString(context.Failure?.Message ?? "unknown")}");
            context.HandleResponse();
            return Task.CompletedTask;
        };
    });
}

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(Policies.IsManager, policy => policy.RequireRole(AppRoles.Manager));
    options.AddPolicy(Policies.IsTechnician, policy => policy.RequireRole(AppRoles.Technician));
    options.AddPolicy(Policies.IsCustomer, policy => policy.RequireRole(AppRoles.Customer));
    options.AddPolicy(Policies.IsStaff, policy => policy.RequireRole(AppRoles.Manager, AppRoles.Technician));
});

// --- OpenAPI (Swagger): /swagger UI for exploring and testing the API from
// the browser. Protected endpoints need a bearer token — paste the access
// token from POST /api/auth/login into the Authorize button.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "Mechanic Shop API", Version = "v1" });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Paste the access token returned by POST /api/auth/login here.",
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
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

// AI diagnostic engine. Default is the offline rule-based MockAIService (works
// out of the box); set AISettings:Provider to "OpenAI" and configure the key to
// use the OpenAI-compatible provider instead. Both are registered; the factory
// below picks the active one so real calls happen only when credentials exist.
builder.Services.AddScoped<OpenAIService>();
builder.Services.AddScoped<MockAIService>();
builder.Services.AddScoped<IAIService>(sp =>
{
    var useOpenAi = string.Equals(
        builder.Configuration["AISettings:Provider"],
        "OpenAI", StringComparison.OrdinalIgnoreCase)
        && !string.IsNullOrWhiteSpace(builder.Configuration["AISettings:OpenAI:ApiKey"]);

    return useOpenAi ? sp.GetRequiredService<OpenAIService>() : sp.GetRequiredService<MockAIService>();
});

var openAiTimeout = TimeSpan.FromSeconds(
    builder.Configuration.GetValue("AISettings:OpenAI:TimeoutSeconds", 30));
builder.Services.AddHttpClient("AI", client => client.Timeout = openAiTimeout);

// --- Health checks + CORS must be registered BEFORE Build(): the service collection
// is read-only afterwards, so anything added later is ignored (or throws).
builder.Services.AddHealthChecks();

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? ["http://localhost", "http://localhost:4200"];

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy => policy
        .WithOrigins(allowedOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod());
});

// --- Rate limiting: brute-force protection for the credential endpoints.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("auth", httpContext => RateLimitPartition.GetFixedWindowLimiter(
        httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = builder.Configuration.GetValue("RateLimiting:AuthPermitPerMinute", 10),
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
        }));
});

// --- Fail fast in Production when secrets were left at their dev defaults.
if (builder.Environment.IsProduction())
{
    var secret = builder.Configuration["JwtSettings:Secret"];
    if (string.IsNullOrWhiteSpace(secret) || secret.Length < 32 || secret.StartsWith("please-change-me", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "JwtSettings:Secret must be set to a random value of at least 32 characters in Production.");
    }
}

var configuredAiProvider = builder.Configuration["AISettings:Provider"] ?? "Mock";
if (!configuredAiProvider.Equals("Mock", StringComparison.OrdinalIgnoreCase) &&
    !configuredAiProvider.Equals("OpenAI", StringComparison.OrdinalIgnoreCase))
{
    throw new InvalidOperationException("AISettings:Provider must be either 'Mock' or 'OpenAI'.");
}
if (configuredAiProvider.Equals("OpenAI", StringComparison.OrdinalIgnoreCase) &&
    string.IsNullOrWhiteSpace(builder.Configuration["AISettings:OpenAI:ApiKey"]))
{
    throw new InvalidOperationException("AISettings:OpenAI:ApiKey is required when AISettings:Provider is 'OpenAI'.");
}

var app = builder.Build();

// Idempotent: only inserts what is not already present, and migrates first.
await SeedData.SeedAsync(
    app.Services,
    includeDemoData: !app.Environment.IsProduction() || app.Configuration.GetValue<bool>("Seed:DemoData"));

app.UseCors("AllowFrontend");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

// Swagger is exposed outside Production only, unless Swagger:Enabled=true.
if (!app.Environment.IsProduction() || app.Configuration.GetValue<bool>("Swagger:Enabled"))
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.RoutePrefix = "swagger";
        options.DocumentTitle = "Mechanic Shop API";
    });
}

app.MapGet("/", () => "Hello World!");
app.MapHealthChecks("/healthz");
app.MapAuthEndpoints();
app.MapUsersEndpoints();
app.MapCustomersEndpoints();
app.MapVehiclesEndpoints();
app.MapServiceTasksEndpoints();
app.MapWorkOrdersEndpoints();
app.MapEmployeesEndpoints();
app.MapAppointmentRequestsEndpoints();
app.MapInvoicesEndpoints();
app.MapPartsEndpoints();
app.MapSuppliersEndpoints();
app.MapPurchaseOrdersEndpoints();
app.MapShopOrdersEndpoints();
app.MapReportsEndpoints();
app.MapNotificationsEndpoints();
app.MapPortalEndpoints();
app.MapAIEndpoints();

app.Run();

// top level statement approach create Program class internal and main method inside it
// so, in integration test we can not access Program class because it is will be internal
// so we do this
// Makes the generated entry point accessible to the integration test assembly.
public partial class Program;
