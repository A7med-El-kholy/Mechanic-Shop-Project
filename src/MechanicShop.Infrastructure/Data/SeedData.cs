using MechanicShop.Infrastructure.Identity;

using System.Security.Cryptography;
using System.Text;

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MechanicShop.Domain.Inventory;
using MechanicShop.Domain.ServiceTasks;
using MechanicShop.Domain.Customers;
using MechanicShop.Domain.Customers.Vehicles;
using MechanicShop.Domain.Employees;
using MechanicShop.Domain.Employees.Enums;
using MechanicShop.Domain.Appointments;
using MechanicShop.Domain.WorkOrders;
using MechanicShop.Domain.WorkOrders.Enums;

namespace MechanicShop.Infrastructure.Data;

/// <summary>
/// Idempotent startup seeding: applies migrations, then the three shop roles, then an
/// optional bootstrap Manager whose credentials come from configuration (user-secrets
/// locally, environment variables when deployed). Nothing is hard-coded in source.
/// Seeding only inserts what is missing, so it is safe to run on every start.
/// </summary>
public static class SeedData
{
    public static async Task SeedAsync(IServiceProvider services, bool includeDemoData)
    {
        using var scope = services.CreateScope();

        // Migrate BEFORE seeding: the AspNetRoles *table* must exist before rows go in.
        // Doing it here keeps the ordering invariant in one place so no caller can get it wrong.
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await db.Database.MigrateAsync();

        // SQL Server Express turns AUTO_CLOSE on for new databases. That unloads
        // the database whenever the last connection closes, so the next request
        // pays a 15–20s recovery cost (and can time out). Keep it online.
        // This requires ALTER DATABASE permission (sa in docker/compose). If the
        // deployment user lacks it we log a warning instead of crashing startup.
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("SeedData");

        try
        {
            await db.Database.ExecuteSqlRawAsync("ALTER DATABASE CURRENT SET AUTO_CLOSE OFF;");
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Could not disable AUTO_CLOSE (continuing anyway).");
        }

        await EnsureRolesAsync(scope, logger);
        await EnsureDefaultManagerAsync(scope, logger);
        if (includeDemoData)
        {
            await EnsureWorkshopCatalogAsync(db, logger);
            await EnsureWorkshopPeopleAsync(db, logger);
        }
    }

    private static async Task EnsureWorkshopCatalogAsync(AppDbContext db, ILogger logger)
    {
        // Catalog-only demo seed: stable IDs and name checks make this repeatable,
        // while existing quantities and prices are never overwritten.
        (string Name, PartCategory Category, decimal Price, int Quantity, int Minimum)[] parts =
        [
            ("Ceramic Front Brake Pad Set", PartCategory.Brakes, 68.50m, 14, 4),
            ("Vented Brake Rotor 320mm", PartCategory.Brakes, 112.00m, 8, 2),
            ("AGM 12V 70Ah Battery", PartCategory.Electrical, 184.99m, 6, 2),
            ("Iridium Spark Plug", PartCategory.Engine, 18.75m, 30, 8),
            ("Engine Oil 5W-30 5L", PartCategory.Fluids, 42.00m, 20, 5),
            ("Oil Filter Cartridge", PartCategory.Engine, 12.40m, 25, 6),
            ("OAT Coolant 5L", PartCategory.Fluids, 27.90m, 3, 5),
            ("Serpentine Belt", PartCategory.Engine, 36.00m, 7, 2),
            ("Engine Air Filter", PartCategory.Engine, 16.50m, 18, 5),
            ("Ignition Coil Pack", PartCategory.Engine, 54.00m, 9, 3),
            ("Timing Belt Kit", PartCategory.Engine, 128.00m, 4, 2),
            ("Fuel Filter", PartCategory.Engine, 22.75m, 11, 3),
            ("Water Pump Assembly", PartCategory.Engine, 96.00m, 5, 2),
            ("Rear Brake Pad Set", PartCategory.Brakes, 59.00m, 10, 3),
            ("DOT 4 Brake Fluid 1L", PartCategory.Fluids, 11.50m, 16, 4),
            ("Alternator Assembly", PartCategory.Electrical, 215.00m, 3, 1),
            ("Starter Motor", PartCategory.Electrical, 176.00m, 4, 1),
            ("Headlamp Bulb H7", PartCategory.Electrical, 14.25m, 22, 6),
            ("Automatic Transmission Fluid 1L", PartCategory.Fluids, 13.80m, 24, 6),
            ("Cabin Air Filter", PartCategory.Engine, 15.00m, 14, 4),
        ];

        var seededParts = new Dictionary<string, Part>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in parts)
        {
            var current = await db.Parts.FirstOrDefaultAsync(p => p.Name == item.Name);
            if (current is null)
            {
                var created = Part.Create(StableId("part", item.Name), item.Name, (int)item.Category,
                    item.Price, item.Quantity, item.Minimum);
                if (created.IsError) throw new InvalidOperationException($"Invalid seed part '{item.Name}'.");
                current = created.Value;
                db.Parts.Add(current);
                db.StockMovements.Add(StockMovement.Create(current.Id, current.Quantity, 0,
                    current.Quantity, "Opening balance (demo seed)"));
            }
            seededParts[item.Name] = current;
        }

        await db.SaveChangesAsync();

        (string Name, decimal Price, int Minutes, string? PartName)[] tasks =
        [
            ("Engine oil and filter service", 89m, 60, "Engine Oil 5W-30 5L"),
            ("Front brake pad replacement", 145m, 90, "Ceramic Front Brake Pad Set"),
            ("Brake rotor and pad replacement", 285m, 150, "Vented Brake Rotor 320mm"),
            ("Battery and charging system test", 55m, 45, "AGM 12V 70Ah Battery"),
            ("Cooling system inspection", 75m, 60, "OAT Coolant 5L"),
            ("Engine diagnostic scan", 65m, 30, null),
            ("Engine air filter replacement", 38m, 30, "Engine Air Filter"),
            ("Ignition coil and misfire diagnosis", 125m, 75, "Ignition Coil Pack"),
            ("Timing belt replacement", 420m, 180, "Timing Belt Kit"),
            ("Fuel delivery inspection", 95m, 60, "Fuel Filter"),
            ("Water pump replacement", 265m, 180, "Water Pump Assembly"),
            ("Rear brake pad replacement", 130m, 90, "Rear Brake Pad Set"),
            ("Brake fluid flush", 68m, 45, "DOT 4 Brake Fluid 1L"),
            ("Alternator replacement", 185m, 120, "Alternator Assembly"),
            ("Starter motor replacement", 160m, 105, "Starter Motor"),
            ("Lighting system repair", 42m, 30, "Headlamp Bulb H7"),
            ("Automatic transmission fluid service", 115m, 75, "Automatic Transmission Fluid 1L"),
            ("Cabin air filter replacement", 34m, 30, "Cabin Air Filter"),
        ];
        foreach (var item in tasks)
        {
            if (await db.ServiceTasks.AnyAsync(t => t.Name == item.Name)) continue;
            var created = ServiceTask.Create(StableId("service", item.Name), item.Name, item.Price, item.Minutes);
            if (created.IsError) throw new InvalidOperationException($"Invalid seed service '{item.Name}'.");
            if (item.PartName is not null) created.Value.SetPart(seededParts[item.PartName].Id);
            db.ServiceTasks.Add(created.Value);
        }

        await db.SaveChangesAsync();
        logger.LogInformation("Ensured workshop catalog seed ({PartCount} parts, {ServiceCount} services).", parts.Length, tasks.Length);

        var supplier = await db.Suppliers.FirstOrDefaultAsync(item => item.Name == "Cairo Auto Parts Supply");
        if (supplier is null)
        {
            var createdSupplier = Supplier.Create(StableId("supplier", "Cairo Auto Parts Supply"),
                "Cairo Auto Parts Supply", "Mariam Khaled", "orders@cairoautoparts.example.test", "+2025550134");
            if (createdSupplier.IsError) throw new InvalidOperationException("Invalid demo supplier.");
            supplier = createdSupplier.Value;
            db.Suppliers.Add(supplier);
            await db.SaveChangesAsync();
        }

        var purchaseOrderId = StableId("purchase-order", "Cairo Auto Parts Supply Open Order");
        if (!await db.PurchaseOrders.AnyAsync(item => item.Id == purchaseOrderId))
        {
            var coolant = await db.Parts.FirstAsync(item => item.Name == "OAT Coolant 5L");
            var pads = await db.Parts.FirstAsync(item => item.Name == "Ceramic Front Brake Pad Set");
            var coolantLine = PurchaseOrderLine.Create(coolant.Id, coolant.Name, 12, 17.50m);
            var padsLine = PurchaseOrderLine.Create(pads.Id, pads.Name, 8, 39.00m);
            if (coolantLine.IsError) throw new InvalidOperationException("Invalid demo purchase order line.");
            if (padsLine.IsError) throw new InvalidOperationException("Invalid demo purchase order line.");
            var order = PurchaseOrder.Create(purchaseOrderId, "PO-DEMO-2026-001", supplier.Id,
                DateOnly.FromDateTime(DateTime.UtcNow), [coolantLine.Value, padsLine.Value]);
            if (order.IsError) throw new InvalidOperationException("Invalid demo purchase order.");
            db.PurchaseOrders.Add(order.Value);
            await db.SaveChangesAsync();
        }
    }

    private static Guid StableId(string kind, string name) =>
        new(SHA256.HashData(Encoding.UTF8.GetBytes($"MechanicShop:{kind}:{name}"))[..16]);

    private static async Task EnsureWorkshopPeopleAsync(AppDbContext db, ILogger logger)
    {
        (string Name, string Phone, string Email, (string Make, string Model, int Year, string Plate, string Vin) Vehicle)[] customers =
        [
            ("Amira Hassan", "+201001234501", "amira.hassan@example.test", ("Toyota", "Corolla", 2021, "CAI-2841", "JTDBR32E720123456")),
            ("Omar Nabil", "+201001234502", "omar.nabil@example.test", ("Hyundai", "Elantra", 2020, "GIZ-7315", "KMHDH4AE5FU123456")),
            ("Nour El-Sayed", "+201001234503", "nour.elsayed@example.test", ("Nissan", "Sunny", 2022, "ALX-5092", "3N1CN7AP8KL123456")),
            ("Karim Fawzy", "+201001234504", "karim.fawzy@example.test", ("Kia", "Sportage", 2019, "CAI-9173", "KNDPMCAC5K7123456")),
        ];
        (string Name, string Make, string Model)[] additionalCustomers =
        [
            ("Leila Mostafa", "Honda", "Civic"), ("Tarek Ibrahim", "Toyota", "Camry"),
            ("Dina Hassan", "Hyundai", "Tucson"), ("Hassan Younis", "Nissan", "Qashqai"),
            ("Farida Adel", "Mazda", "3"), ("Ziad Farouk", "Ford", "Focus"),
            ("Salma Magdy", "Chevrolet", "Cruze"), ("Yassin Reda", "Volkswagen", "Golf"),
            ("Mariam Emad", "Skoda", "Octavia"), ("Fady Naguib", "Mercedes-Benz", "C-Class"),
            ("Jana Sherif", "BMW", "3 Series"), ("Mostafa Galal", "Renault", "Logan"),
            ("Nadine Hatem", "Mitsubishi", "Lancer"), ("Adham Kamal", "Peugeot", "301"),
            ("Rana Sameh", "Suzuki", "Swift"), ("Bassem Atef", "Toyota", "RAV4"),
            ("Hala Saeed", "Hyundai", "Accent"), ("Amr Wahid", "Nissan", "X-Trail"),
            ("Yara Essam", "Kia", "Cerato"), ("Fares Mahmoud", "Honda", "CR-V"),
            ("Nada Samir", "Chery", "Tiggo 7"), ("Ramy Medhat", "MG", "ZS"),
            ("Laila Roshdy", "BYD", "F3"), ("Mazen Shaker", "Opel", "Astra"),
        ];
        var allCustomers = customers.Concat(additionalCustomers.Select((item, index) =>
        {
            var number = index + 1;
            return (
                Name: item.Name,
                Phone: $"+20255501{number + 39:00}",
                Email: $"demo.customer.{number:00}@example.test",
                Vehicle: (Make: item.Make, Model: item.Model, Year: 2018 + number % 8,
                    Plate: $"DEM-{number:0000}", Vin: $"1HGCM82633A{number:000000}"));
        })).ToArray();

        foreach (var item in allCustomers)
        {
            if (await db.Customers.AnyAsync(c => c.Email == item.Email)) continue;
            var customerResult = Customer.Create(StableId("customer", item.Email), item.Name, item.Phone, item.Email, []);
            if (customerResult.IsError) throw new InvalidOperationException($"Invalid seed customer '{item.Email}'.");
            var vehicleResult = Vehicle.Create(StableId("vehicle", item.Vehicle.Vin), customerResult.Value.Id,
                item.Vehicle.Make, item.Vehicle.Model, item.Vehicle.Year, item.Vehicle.Plate, item.Vehicle.Vin);
            if (vehicleResult.IsError) throw new InvalidOperationException($"Invalid seed vehicle for '{item.Email}'.");
            customerResult.Value.AddVehicle(vehicleResult.Value);
            db.Customers.Add(customerResult.Value);
        }

        var technicians = new[] { "Mina Adel", "Sara Mahmoud", "Youssef Samir", "Hany George", "Menna Ali", "Peter Sameh", "Nour Ezzat", "Omar Kamal" };
        foreach (var name in technicians)
        {
            if (await db.Employees.AnyAsync(e => e.FullName == name)) continue;
            var result = Employee.Create(StableId("employee", name), name, EmployeeRole.Technician);
            if (result.IsError) throw new InvalidOperationException($"Invalid seed technician '{name}'.");
            db.Employees.Add(result.Value);
        }

        await db.SaveChangesAsync();

        (string Plate, string Technician, string Service, int DaysAhead, int StartMinute, Spot Spot)[] jobs =
        [
            ("CAI-2841", "Mina Adel", "Engine oil and filter service", 1, 540, Spot.A),
            ("GIZ-7315", "Sara Mahmoud", "Front brake pad replacement", 2, 600, Spot.B),
            ("ALX-5092", "Youssef Samir", "Engine diagnostic scan", 3, 660, Spot.C),
        ];
        var demoJobs = jobs.Concat(additionalCustomers.Select((customer, index) =>
        {
            var technician = technicians[index % technicians.Length];
            var service = new[]
            {
                "Engine air filter replacement", "Ignition coil and misfire diagnosis", "Timing belt replacement",
                "Fuel delivery inspection", "Water pump replacement", "Rear brake pad replacement",
                "Brake fluid flush", "Alternator replacement", "Starter motor replacement", "Lighting system repair",
                "Automatic transmission fluid service", "Cabin air filter replacement", "Engine diagnostic scan",
            }[index % 13];
            return (Plate: $"DEM-{index + 1:0000}", Technician: technician, Service: service,
                DaysAhead: index + 7, StartMinute: 540 + (index % 3) * 120, Spot: (Spot)(index % 3));
        })).ToArray();

        foreach (var job in demoJobs)
        {
            var vehicle = await db.Vehicles.FirstAsync(item => item.LicensePlate == job.Plate);
            var technician = await db.Employees.FirstAsync(item => item.FullName == job.Technician);
            var service = await db.ServiceTasks.FirstAsync(item => item.Name == job.Service);
            var id = StableId("work-order", job.Plate);
            if (!await db.WorkOrders.AnyAsync(item => item.Id == id))
            {
                var workOrder = WorkOrder.Create(id, vehicle.Id, [service],
                    DateOnly.FromDateTime(DateTime.UtcNow).AddDays(job.DaysAhead),
                    job.StartMinute, job.Spot, technician.Id);
                if (workOrder.IsError) throw new InvalidOperationException($"Invalid seed work order for '{job.Plate}'.");
                db.WorkOrders.Add(workOrder.Value);
            }
        }

        (string Plate, string Service, int DaysAhead, string Notes, ReportedUrgency Urgency)[] requests =
        [
            ("CAI-2841", "Engine diagnostic scan", 4, "Intermittent engine warning light; please inspect before a long trip.", ReportedUrgency.Medium),
            ("GIZ-7315", "Cooling system inspection", 5, "Temperature rises in traffic; no visible leak noticed.", ReportedUrgency.High),
            ("KNDPMCAC5K7123456", "Battery and charging system test", 6, "Battery sometimes struggles to start in the morning.", ReportedUrgency.Low),
        ];
        var demoRequests = requests.Concat(additionalCustomers.Take(12).Select((customer, index) =>
        {
            var services = new[]
            {
                "Engine diagnostic scan", "Cooling system inspection", "Battery and charging system test",
                "Brake fluid flush", "Ignition coil and misfire diagnosis", "Fuel delivery inspection",
            };
            var concerns = new[]
            {
                "Warning light appears intermittently during the morning commute.",
                "Please inspect an unusual noise before the next long drive.",
                "Vehicle needs a routine check and fluid condition review.",
                "Starting performance has become inconsistent over the last week.",
            };
            return (Plate: $"DEM-{index + 1:0000}", Service: services[index % services.Length], DaysAhead: index + 20,
                Notes: concerns[index % concerns.Length], Urgency: (ReportedUrgency)(index % 3));
        })).ToArray();

        foreach (var request in demoRequests)
        {
            var vehicle = await db.Vehicles.FirstAsync(item => item.LicensePlate == request.Plate || item.Vin == request.Plate);
            if (await db.AppointmentRequests.AnyAsync(item => item.Id == StableId("appointment", request.Plate))) continue;
            var service = await db.ServiceTasks.FirstAsync(item => item.Name == request.Service);
            var appointment = AppointmentRequest.Create(StableId("appointment", request.Plate), vehicle.CustomerId,
                vehicle.Id, service.Id, DateOnly.FromDateTime(DateTime.UtcNow).AddDays(request.DaysAhead),
                request.Notes, request.Urgency);
            if (appointment.IsError) throw new InvalidOperationException($"Invalid seed appointment request for '{request.Plate}'.");
            db.AppointmentRequests.Add(appointment.Value);
        }

        await db.SaveChangesAsync();
        logger.LogInformation("Ensured workshop demo customers, vehicles, and technicians.");
    }

    private static async Task EnsureRolesAsync(IServiceScope scope, ILogger logger)
    {
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        foreach (var role in AppRoles.All)
        {
            if (await roleManager.RoleExistsAsync(role))
            {
                continue;
            }

            var result = await roleManager.CreateAsync(new IdentityRole(role));

            if (result.Succeeded)
            {
                logger.LogInformation("Seeded role {Role}", role);
            }
            else
            {
                var errors = string.Join("; ", result.Errors.Select(e => e.Description));

                throw new InvalidOperationException($"Failed to seed role '{role}': {errors}");
            }
        }
    }

    private static async Task EnsureDefaultManagerAsync(IServiceScope scope, ILogger logger)
    {
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();

        var email = configuration["Seed:ManagerEmail"];
        var password = configuration["Seed:ManagerPassword"];

        // Not configured = no bootstrap account; deployment decides intentionally.
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        if (await userManager.FindByEmailAsync(email) is not null)
        {
            return; // already provisioned — stay idempotent on re-runs
        }

        var manager = new AppUser
        {
            UserName = email,
            Email = email,
            FullName = "Shop Manager",
            EmailConfirmed = true,
        };

        var createResult = await userManager.CreateAsync(manager, password);

        if (!createResult.Succeeded)
        {
            var errors = string.Join("; ", createResult.Errors.Select(e => e.Description));

            throw new InvalidOperationException($"Failed to seed the bootstrap manager: {errors}");
        }

        var roleResult = await userManager.AddToRoleAsync(manager, AppRoles.Manager);

        if (!roleResult.Succeeded)
        {
            var errors = string.Join("; ", roleResult.Errors.Select(e => e.Description));

            throw new InvalidOperationException($"Failed to give the bootstrap manager its role: {errors}");
        }

        logger.LogInformation("Seeded bootstrap manager {Email}", email);
    }
}
