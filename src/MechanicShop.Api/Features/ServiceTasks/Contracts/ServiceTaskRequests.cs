namespace MechanicShop.Api.Features.ServiceTasks.Contracts;

/// <summary>Body for POST /api/service-tasks. Duration is sent as whole minutes.</summary>
public sealed record CreateServiceTaskRequest(
    string Name,
    decimal BasePrice,
    int EstimatedDurationInMins);

/// <summary>Body for PUT /api/service-tasks/{id}.</summary>
public sealed record UpdateServiceTaskRequest(
    string Name,
    decimal BasePrice,
    int EstimatedDurationInMins);
