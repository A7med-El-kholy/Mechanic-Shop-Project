using MechanicShop.Domain.Common;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.ServiceTasks.Enums;

namespace MechanicShop.Domain.ServiceTasks;

/// <summary>
/// A catalog entry: what the workshop can do, for how long, at what starting price.
/// Work orders (F4) will reference these instead of free-typing names, which keeps
/// reporting and scheduling consistent.
/// </summary>
public sealed class ServiceTask : Entity
{
    public string Name { get; private set; }

    public decimal BasePrice { get; private set; }

    public ServiceDurationInMinutes EstimatedDurationInMins { get; private set; }

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
}
