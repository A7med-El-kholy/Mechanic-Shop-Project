using MechanicShop.Application.Features.Employees.Dtos;
using MechanicShop.Domain.Common.Results;

using MediatR;

namespace MechanicShop.Application.Features.Employees.Queries.GetEmployees;

/// <summary>Full roster for the staff Employees screen.</summary>
public sealed record GetEmployeesQuery()
    : IRequest<Result<List<EmployeeDto>>>;
