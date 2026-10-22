using MechanicShop.Application.Features.Employees.Dtos;
using MechanicShop.Domain.Common.Results;

using MediatR;

namespace MechanicShop.Application.Features.Employees.Queries.GetEmployeeById;

public sealed record GetEmployeeByIdQuery(Guid EmployeeId)
    : IRequest<Result<EmployeeDto>>;
