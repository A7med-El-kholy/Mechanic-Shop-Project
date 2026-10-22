using MechanicShop.Api.Common;
using MechanicShop.Api.Features.Employees.Contracts;
using MechanicShop.Application.Features.Employees.Commands.CreateEmployee;
using MechanicShop.Application.Features.Employees.Commands.RemoveEmployee;
using MechanicShop.Application.Features.Employees.Queries.GetEmployeeById;
using MechanicShop.Application.Features.Employees.Queries.GetEmployees;
using MechanicShop.Application.Features.Employees.Queries.GetTechnicians;
using MechanicShop.Domain.Common.Results;

using MediatR;

namespace MechanicShop.Api.Features.Employees;

/// <summary>
/// The workshop roster: the technicians and managers scheduling works with.
/// Accounts/permissions stay in Identity; this list is what bookings cite.
/// </summary>
public static class EmployeesEndpoints
{
    public static IEndpointRouteBuilder MapEmployeesEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/employees").WithTags("Employees");

        group.MapGet("/", ListAsync)
            .WithName("GetEmployees")
            .RequireAuthorization(Policies.IsStaff)
            .Produces(StatusCodes.Status200OK);

        group.MapGet("/technicians", ListTechniciansAsync)
            .WithName("GetTechnicians")
            .RequireAuthorization(Policies.IsStaff)
            .Produces(StatusCodes.Status200OK);

        group.MapGet("/{employeeId:guid}", GetAsync)
            .WithName("GetEmployeeById")
            .RequireAuthorization(Policies.IsStaff)
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/", CreateAsync)
            .WithName("CreateEmployee")
            .RequireAuthorization(Policies.IsStaff)
            .Produces(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapDelete("/{employeeId:guid}", DeleteAsync)
            .WithName("RemoveEmployee")
            .RequireAuthorization(Policies.IsStaff)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return endpoints;
    }

    private static async Task<IResult> ListAsync(
        ISender sender,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetEmployeesQuery(), cancellationToken);

        return result.IsError
            ? result.TopError.ToProblem(httpContext)
            : Results.Ok(result.Value);
    }

    private static async Task<IResult> ListTechniciansAsync(
        ISender sender,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetTechniciansQuery(), cancellationToken);

        return result.IsError
            ? result.TopError.ToProblem(httpContext)
            : Results.Ok(result.Value);
    }

    private static async Task<IResult> GetAsync(
        Guid employeeId,
        ISender sender,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetEmployeeByIdQuery(employeeId), cancellationToken);

        return result.IsError
            ? result.TopError.ToProblem(httpContext)
            : Results.Ok(result.Value);
    }

    private static async Task<IResult> CreateAsync(
        CreateEmployeeRequest request,
        ISender sender,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new CreateEmployeeCommand(request.FullName, request.Role),
            cancellationToken);

        return result.IsError
            ? result.TopError.ToProblem(httpContext)
            : Results.Created($"/api/employees/{result.Value.EmployeeId}", result.Value);
    }

    private static async Task<IResult> DeleteAsync(
        Guid employeeId,
        ISender sender,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new RemoveEmployeeCommand(employeeId), cancellationToken);

        return result.IsError
            ? result.TopError.ToProblem(httpContext)
            : Results.NoContent();
    }
}
