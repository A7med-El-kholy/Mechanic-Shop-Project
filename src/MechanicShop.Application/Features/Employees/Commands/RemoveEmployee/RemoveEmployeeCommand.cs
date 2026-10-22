using MechanicShop.Application.Features.Employees.Dtos;
using MechanicShop.Domain.Common.Results;

using MediatR;

namespace MechanicShop.Application.Features.Employees.Commands.RemoveEmployee;

public sealed record RemoveEmployeeCommand(Guid EmployeeId)
    : IRequest<Result<Deleted>>;
