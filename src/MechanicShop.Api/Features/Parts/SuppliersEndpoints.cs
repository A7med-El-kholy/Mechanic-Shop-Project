using MechanicShop.Api.Common;
using MechanicShop.Api.Features.Parts.Contracts;
using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.Parts.Commands.CreateSupplier;
using MechanicShop.Application.Features.Parts.Queries.GetSuppliers;
using MediatR;

namespace MechanicShop.Api.Features.Parts;

public static class SuppliersEndpoints
{
    public static IEndpointRouteBuilder MapSuppliersEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/suppliers").WithTags("Suppliers");
        group.MapGet("/", ListAsync).RequireAuthorization(Policies.IsStaff).WithName("GetSuppliers");
        group.MapPost("/", CreateAsync).RequireAuthorization(Policies.IsManager).WithName("CreateSupplier")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);
        return endpoints;
    }

    private static async Task<IResult> ListAsync(ISender sender, CancellationToken cancellationToken) =>
        Results.Ok(await sender.Send(new GetSuppliersQuery(), cancellationToken));

    private static async Task<IResult> CreateAsync(CreateSupplierRequest request, ISender sender,
        HttpContext httpContext, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new CreateSupplierCommand(request.Name, request.ContactName, request.Email, request.Phone), cancellationToken);
        return result.IsError ? result.TopError.ToProblem(httpContext) : Results.Created($"/api/suppliers/{result.Value.SupplierId}", result.Value);
    }
}
