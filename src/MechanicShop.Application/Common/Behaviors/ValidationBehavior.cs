using FluentValidation;

using MediatR;

using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.Common.Results.Abstractions;

namespace MechanicShop.Application.Common.Behaviors;

/// <summary>
/// MediatR pipeline step that runs the FluentValidation validator (if one is
/// registered for the request) BEFORE the handler executes. On failure it short-
/// circuits and returns a failed Result, so handlers only ever see valid shapes.
/// The (dynamic) cast works because Result&lt;T&gt; has an implicit conversion from
/// List&lt;Error&gt; — the same trick the reference solution uses.
/// </summary>
public sealed class ValidationBehavior<TRequest, TResponse>(
    IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
    where TResponse : IResult
{
    private readonly IEnumerable<IValidator<TRequest>> _validators = validators;

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var failures = _validators
            .Select(v => v.Validate(request))
            .SelectMany(result => result.Errors)
            .Where(failure => failure is not null)
            .ToList();

        if (failures.Count == 0)
        {
            return await next(cancellationToken);
        }

        var errors = failures.ConvertAll(failure => Error.Validation(
            code: failure.PropertyName,
            description: failure.ErrorMessage));

        return (dynamic)errors;
    }
}
