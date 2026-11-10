namespace MechanicShop.Domain.Appointments;

using MechanicShop.Domain.Customers;
using MechanicShop.Domain.Customers.Vehicles;
using MechanicShop.Domain.ServiceTasks;
using MechanicShop.Domain.Common.Results;

/// <summary>Where a request sits on its way to becoming a real booking.</summary>
public enum AppointmentRequestStatus
{
    /// <summary>Sent by the customer; the workshop has not confirmed a slot yet.</summary>
    Pending = 0,
}

/// <summary>
/// The urgency the customer's assistant session CLAIMED for this request
/// (Low / Medium / High). It is a report, never an instruction: scheduling and
/// priority stay human decisions — the chip only tells the reviewer what was
/// said. Null means nobody assessed urgency (a plainly filled request form).
/// </summary>
public enum ReportedUrgency
{
    Low = 0,

    Medium = 1,

    High = 2,
}

/// <summary>The customer-facing half of scheduling: a customer describes WHAT they
/// need and WHEN they would like it, without consuming a technician or a bay.
/// Booking (work order) remains a staff decision — same rule as the prototype:
/// a request is not a booking.</summary>
public sealed class AppointmentRequest
{
    public Guid Id { get; private set; }

    public Guid CustomerId { get; private set; }

    public Guid VehicleId { get; private set; }

    /// <summary>Null means the customer answered "Not sure — inspection".</summary>
    public Guid? ServiceTaskId { get; private set; }

    /// <summary>Date-only vocabulary on purpose: the shop grid is date + minute,
    /// and a preferred date needs no timezone reasoning.</summary>
    public DateOnly PreferredDate { get; private set; }

    /// <summary>What the customer said, verbatim-ish: the description plus the
    /// guided conversation behind it. Empty for plain form requests.</summary>
    public string Notes { get; private set; } = string.Empty;

    public ReportedUrgency? Urgency { get; private set; }

    public AppointmentRequestStatus Status { get; private set; }

    /// <summary>The conversation record's bound — a description plus a few
    /// question/answer lines must never become an unbounded free-text dump.</summary>
    public const int NotesMaxLength = 2000;

    // Navigations for projections (owner/car/service names); null until included.
    public Customer? Customer { get; private set; }

    public Vehicle? Vehicle { get; private set; }

    public ServiceTask? ServiceTask { get; private set; }

    private AppointmentRequest()
    {
    }

    public static Result<AppointmentRequest> Create(
        Guid id,
        Guid customerId,
        Guid vehicleId,
        Guid? serviceTaskId,
        DateOnly preferredDate,
        string? notes = null,
        ReportedUrgency? urgency = null)
    {
        if (id == Guid.Empty)
        {
            return AppointmentRequestErrors.IdRequired;
        }

        if (customerId == Guid.Empty)
        {
            return AppointmentRequestErrors.CustomerIdRequired;
        }

        if (vehicleId == Guid.Empty)
        {
            return AppointmentRequestErrors.VehicleIdRequired;
        }

        if (serviceTaskId == Guid.Empty)
        {
            return AppointmentRequestErrors.ServiceTaskIdRequired;
        }

        if (preferredDate < DateOnly.FromDateTime(DateTime.UtcNow))
        {
            return AppointmentRequestErrors.DateInPast;
        }

        if (notes is { Length: > NotesMaxLength })
        {
            return AppointmentRequestErrors.NotesTooLong;
        }

        ReportedUrgency? reported = null;

        if (urgency is { } value)
        {
            if (value is not (ReportedUrgency.Low or ReportedUrgency.Medium or ReportedUrgency.High))
            {
                return AppointmentRequestErrors.UrgencyInvalid;
            }

            reported = value;
        }

        var request = new AppointmentRequest
        {
            Id = id,
            CustomerId = customerId,
            VehicleId = vehicleId,
            ServiceTaskId = serviceTaskId,
            PreferredDate = preferredDate,
            Notes = notes?.Trim() ?? string.Empty,
            Urgency = reported,
            Status = AppointmentRequestStatus.Pending,
        };

        return request;
    }
}
