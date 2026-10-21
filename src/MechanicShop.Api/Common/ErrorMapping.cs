using MechanicShop.Domain.Common.Results;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace MechanicShop.Api.Common;

/// <summary>
/// Translates the Domain's <see cref="Error"/> into RFC 7807 ProblemDetails responses.
/// This is the single place where ErrorKind decides the HTTP status code, so every
/// endpoint reports failures the same way.
/// </summary>
public static class ErrorMapping
{
    public static IResult ToProblem(this Error error, HttpContext httpContext)
    {
        var (statusCode, title) = error.Type switch
        {
            ErrorKind.Validation => (StatusCodes.Status400BadRequest, "Validation failed"),
            ErrorKind.NotFound => (StatusCodes.Status404NotFound, "Resource not found"),
            ErrorKind.Conflict => (StatusCodes.Status409Conflict, "Conflict"),
            ErrorKind.Unauthorized => (StatusCodes.Status401Unauthorized, "Unauthorized"),
            ErrorKind.Forbidden => (StatusCodes.Status403Forbidden, "Forbidden"),
            _ => (StatusCodes.Status500InternalServerError, "Unexpected error"),
        };

        // Failure/Unexpected are genuinely server-side; we deliberately do not echo
        // internal messages to the client for those.
        var detail = error.Type is ErrorKind.Failure or ErrorKind.Unexpected
            ? title
            : error.Description;

        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Instance = httpContext.Request.Path,
            Extensions = { ["code"] = error.Code },
        };

        return Results.Problem(problem);
    }
}
