namespace MechanicShop.Api.Features.ServiceTasks.Contracts;

/// <summary>Body for POST /api/service-tasks. Duration is sent as whole minutes.</summary>
public sealed record CreateServiceTaskRequest(
    string Name,
    decimal BasePrice,
    int EstimatedDurationInMins);

/// <summary>Body for PUT /api/service-tasks/{id}. PartId is OPTIONAL:
/// absent keeps the current link, an explicit null (or Guid.Empty) clears it,
/// a real id re-points the task at another shelf.</summary>
public sealed record UpdateServiceTaskRequest(
    string Name,
    decimal BasePrice,
    int EstimatedDurationInMins,
    Guid? PartId = null);
