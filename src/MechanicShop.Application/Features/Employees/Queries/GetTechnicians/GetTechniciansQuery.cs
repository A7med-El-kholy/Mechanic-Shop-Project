using MechanicShop.Application.Features.Employees.Dtos;
using MechanicShop.Domain.Common.Results;

using MediatR;

namespace MechanicShop.Application.Features.Employees.Queries.GetTechnicians;

/// <summary>The scheduling form's technician dropdown source.</summary>
public sealed record GetTechniciansQuery()
    : IRequest<Result<List<EmployeeDto>>>;
