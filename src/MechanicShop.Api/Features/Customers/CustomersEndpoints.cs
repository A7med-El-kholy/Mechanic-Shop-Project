using MechanicShop.Api.Common;
using MechanicShop.Api.Features.Customers.Contracts;
using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.Customers.Commands.CreateCustomer;
using MechanicShop.Application.Features.Customers.Commands.RemoveCustomer;
using MechanicShop.Application.Features.Customers.Commands.UpdateCustomer;
using MechanicShop.Application.Features.Customers.Queries.GetCustomerById;
using MechanicShop.Application.Features.Customers.Queries.GetCustomers;
using MechanicShop.Domain.Common.Results;

using MediatR;

namespace MechanicShop.Api.Features.Customers;

/// <summary>
/// Customer CRUD for shop staff (Manager or Technician). Customer-role users manage
/// their own records through a self-access policy that lands with their profile link
/// in a later slice.
/// </summary>
public static class CustomersEndpoints
{
    public static IEndpointRouteBuilder MapCustomersEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/customers").WithTags("Customers");

        group.MapPost("/", CreateAsync)
            .WithName("CreateCustomer")
            .RequireAuthorization(Policies.IsStaff)
            .Produces(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapGet("/", ListAsync)
            .WithName("GetCustomers")
            .RequireAuthorization(Policies.IsStaff)
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapGet("/{customerId:guid}", GetAsync)
            .WithName("GetCustomerById")
            .RequireAuthorization(Policies.IsStaff)
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/{customerId:guid}", UpdateAsync)
            .WithName("UpdateCustomer")
            .RequireAuthorization(Policies.IsStaff)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapDelete("/{customerId:guid}", DeleteAsync)
            .WithName("RemoveCustomer")
            .RequireAuthorization(Policies.IsStaff)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return endpoints;
    }

    private static async Task<IResult> CreateAsync(
        CreateCustomerRequest request,
        ISender sender,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new CreateCustomerCommand(
                request.Name,
                request.PhoneNumber,
                request.Email,
                [.. request.Vehicles.Select(v => new CreateVehiclePayload(
                    v.Make,
                    v.Model,
                    v.Year,
                    v.LicensePlate,
                    v.Vin))]),
            cancellationToken);

        return result.IsError
            ? result.TopError.ToProblem(httpContext)
            : Results.Created($"/api/customers/{result.Value.CustomerId}", result.Value);
    }

    private static async Task<IResult> ListAsync(
        ISender sender,
        int page = 1,
        int pageSize = 10,
        string? search = null,
        HttpContext httpContext = null!,
        CancellationToken cancellationToken = default)
    {
        var result = await sender.Send(
            new GetCustomersQuery(page, pageSize, search),
            cancellationToken);

        return result.IsError
            ? result.TopError.ToProblem(httpContext)
            : Results.Ok(result.Value);
    }

    private static async Task<IResult> GetAsync(
        Guid customerId,
        ISender sender,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new GetCustomerByIdQuery(customerId),
            cancellationToken);

        return result.IsError
            ? result.TopError.ToProblem(httpContext)
            : Results.Ok(result.Value);
    }

    private static async Task<IResult> UpdateAsync(
        Guid customerId,
        UpdateCustomerRequest request,
        ISender sender,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new UpdateCustomerCommand(customerId, request.Name, request.Email, request.PhoneNumber),
            cancellationToken);

        return result.IsError
            ? result.TopError.ToProblem(httpContext)
            : Results.NoContent();
    }

    private static async Task<IResult> DeleteAsync(
        Guid customerId,
        ISender sender,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new RemoveCustomerCommand(customerId),
            cancellationToken);

        return result.IsError
            ? result.TopError.ToProblem(httpContext)
            : Results.NoContent();
    }
}
