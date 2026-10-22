using MechanicShop.Domain.Customers.Vehicles;
using MechanicShop.Domain.ServiceTasks;

using Microsoft.EntityFrameworkCore;

namespace MechanicShop.Application.Common.Errors;

/// <summary>
/// Detects database-enforced uniqueness violations that cannot be caught by
/// application-level existence checks (e.g. a concurrent insert racing the check).
/// SQL Server reports unique index violations with errors 2601/2627 and includes
/// the index name in the message, so we match on the known index name.
/// </summary>
public static class DatabaseErrors
{
    /// <summary>Must match the index name configured in VehicleConfiguration.</summary>
    public const string VehicleVinIndexName = "IX_Vehicles_Vin";

    public const string VehicleLicensePlateIndexName = "IX_Vehicles_LicensePlate";

    public const string ServiceTaskNameIndexName = "IX_ServiceTasks_Name";

    /// <summary>Detects a race that lost a unique ServiceTask name insert.</summary>
    public static bool IsServiceTaskNameUniqueViolation(DbUpdateException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        if (exception.Entries.Any(e => e.Entity is ServiceTask) is false)
        {
            return false;
        }

        return InnerMessagesMention(exception, ServiceTaskNameIndexName);
    }

    public static bool IsVehicleVinUniqueViolation(DbUpdateException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        if (exception.Entries.Any(e => e.Entity is Vehicle) is false)
        {
            return false;
        }

        return InnerMessagesMention(exception, VehicleVinIndexName);
    }

    public static bool IsVehicleLicensePlateUniqueViolation(DbUpdateException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        if (exception.Entries.Any(e => e.Entity is Vehicle) is false)
        {
            return false;
        }

        return InnerMessagesMention(exception, VehicleLicensePlateIndexName);
    }

    private static bool InnerMessagesMention(DbUpdateException exception, string indexName)
    {
        for (var inner = exception.InnerException; inner is not null; inner = inner.InnerException)
        {
            if (inner.Message.Contains(indexName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
