using MechanicShop.Domain.Common.Results;

using MediatR;

namespace MechanicShop.Application.Features.Customers.Commands.UpdateMyCustomer;

/// <summary>
/// A signed-in customer edits their own profile. Email is intentionally not part
/// of this command: it is the login identity and must stay staff-controlled until
/// an email-change verification flow exists.
/// </summary>
public sealed record UpdateMyCustomerCommand(
    string Name,
    string PhoneNumber)
    : IRequest<Result<Updated>>;
