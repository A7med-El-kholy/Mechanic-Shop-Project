using MechanicShop.Application.Features.Employees.Dtos;
using MechanicShop.Domain.Common.Results;

using MediatR;

namespace MechanicShop.Application.Features.Employees.Commands.CreateEmployee;

public sealed record CreateEmployeeCommand(
    string FullName,
    int Role)
    : IRequest<Result<EmployeeDto>>;
