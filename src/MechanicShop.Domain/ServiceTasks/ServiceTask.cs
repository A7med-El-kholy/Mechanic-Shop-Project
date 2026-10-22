using MechanicShop.Domain.Common;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.Inventory;
using MechanicShop.Domain.ServiceTasks.Enums;

namespace MechanicShop.Domain.ServiceTasks;

/// <summary>
/// A catalog entry: what the workshop can do, for how long, at what starting price.
/// Work orders (F4) will reference these instead of free-typing names, which keeps
/// reporting and scheduling consistent. Optionally consumes ONE kind of part —
/// completing a job that cites it deducts a unit from the shelf (stock rules F9).
/// </summary>
public sealed class ServiceTask : Entity
{
    public string Name { get; private set; }

    public decimal BasePrice { get; private set; }

    public ServiceDurationInMinutes EstimatedDurationInMins { get; private set; }

    /// <summary>The "main part" this job consumes. Nullable — labor-only jobs
    /// exist. Lives on the task because the CONSUMPTION is the job's property.</summary>
    public Guid? PartId { get; private set; }

    public Part? Part { get; private set; }

#pragma warning disable CS8618

    private ServiceTask()
    { }

#pragma warning restore CS8618

    private ServiceTask(Guid id, string name, decimal basePrice, ServiceDurationInMinutes estimatedDurationInMins)
        : base(id)
    {
        Name = name;
        BasePrice = basePrice;
        EstimatedDurationInMins = estimatedDurationInMins;
    }

    public static Result<ServiceTask> Create(
        Guid id,
        string name,
        decimal basePrice,
        int estimatedDurationInMins)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return ServiceTaskErrors.NameRequired;
        }

        if (name.Trim().Length > 150)
        {
            return ServiceTaskErrors.NameTooLong;
        }

        if (basePrice <= 0 || basePrice > 10_000)
        {
            return ServiceTaskErrors.BasePriceInvalid;
        }

        if (Enum.IsDefined(typeof(ServiceDurationInMinutes), estimatedDurationInMins) is false)
        {
            return ServiceTaskErrors.DurationInvalid;
        }

        return new ServiceTask(
            id,
            name.Trim(),
            basePrice,
            (ServiceDurationInMinutes)estimatedDurationInMins);
    }

    public Result<Updated> Update(string name, decimal basePrice, int estimatedDurationInMins)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return ServiceTaskErrors.NameRequired;
        }

        if (name.Trim().Length > 150)
        {
            return ServiceTaskErrors.NameTooLong;
        }

        if (basePrice <= 0 || basePrice > 10_000)
        {
            return ServiceTaskErrors.BasePriceInvalid;
        }

        if (Enum.IsDefined(typeof(ServiceDurationInMinutes), estimatedDurationInMins) is false)
        {
            return ServiceTaskErrors.DurationInvalid;
        }

        Name = name.Trim();
        BasePrice = basePrice;
        EstimatedDurationInMins = (ServiceDurationInMinutes)estimatedDurationInMins;

        return Result.Updated;
    }

    /// <summary>Points the task at the part it consumes (null clears it).
    /// Existence of the part is the handler's check, not the entity's.</summary>
    public Result<Updated> SetPart(Guid? partId)
    {
        PartId = partId;

        return Result.Updated;
    }
}
