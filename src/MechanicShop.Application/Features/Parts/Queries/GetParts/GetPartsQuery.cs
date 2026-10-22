using MechanicShop.Application.Features.Parts.Dtos;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.Inventory;

using MediatR;

namespace MechanicShop.Application.Features.Parts.Queries.GetParts;

/// <summary>The shelf list — the storeroom table for staff and the shop grid
/// for customers are views of the same data, so one query feeds both.</summary>
public sealed record GetPartsQuery : IRequest<Result<List<PartDto>>>;
