using MechanicShop.Api.Common;
using MechanicShop.Api.Features.Notifications.Contracts;

using MechanicShop.Application.Features.Notifications.Commands.MarkNotificationsRead;
using MechanicShop.Application.Features.Notifications.Commands.SetNotificationReadState;
using MechanicShop.Application.Features.Notifications.Queries.GetNotifications;
using MechanicShop.Application.Features.Notifications.Queries.GetUnreadNotificationCount;

using MechanicShop.Domain.Notifications.Enums;

using MediatR;

namespace MechanicShop.Api.Features.Notifications;

/// <summary>
/// The staff inbox. Notifications are never posted here — business handlers write
/// them inside the same save as the fact they describe, so an inbox can never
/// disagree with the workshop. The portal mounts the same four operations under
/// /api/portal/notifications with the Customer scope; the data wall lives in the
/// handlers, not in this file.
/// </summary>
public static class NotificationsEndpoints
{
    public static IEndpointRouteBuilder MapNotificationsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/notifications").WithTags("Notifications");

        group.MapGet("/", ListAsync)
            .WithName("GetNotifications")
            .RequireAuthorization(Policies.IsStaff)
            .Produces(StatusCodes.Status200OK);

        group.MapGet("/unread-count", UnreadCountAsync)
            .WithName("GetUnreadNotificationCount")
            .RequireAuthorization(Policies.IsStaff)
            .Produces(StatusCodes.Status200OK);

        group.MapPut("/{notificationId:guid}/read-state", SetReadStateAsync)
            .WithName("SetNotificationReadState")
            .RequireAuthorization(Policies.IsStaff)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/read-all", MarkAllReadAsync)
            .WithName("MarkNotificationsRead")
            .RequireAuthorization(Policies.IsStaff)
            .Produces(StatusCodes.Status204NoContent);

        return endpoints;
    }

    private static async Task<IResult> ListAsync(
        bool unreadOnly = false,
        int page = 1,
        int pageSize = 20,
        ISender sender = null!,
        HttpContext httpContext = null!,
        CancellationToken cancellationToken = default)
    {
        var result = await sender.Send(
            new GetNotificationsQuery(NotificationAudience.Staff, unreadOnly, page, pageSize),
            cancellationToken);

        return result.IsError
            ? result.TopError.ToProblem(httpContext)
            : Results.Ok(result.Value);
    }

    private static async Task<IResult> UnreadCountAsync(
        ISender sender,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new GetUnreadNotificationCountQuery(NotificationAudience.Staff),
            cancellationToken);

        return result.IsError
            ? result.TopError.ToProblem(httpContext)
            : Results.Ok(new UnreadNotificationsResponse(result.Value));
    }

    private static async Task<IResult> SetReadStateAsync(
        Guid notificationId,
        SetNotificationReadStateRequest request,
        ISender sender,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new SetNotificationReadStateCommand(
                notificationId,
                request.IsRead,
                NotificationAudience.Staff),
            cancellationToken);

        return result.IsError
            ? result.TopError.ToProblem(httpContext)
            : Results.NoContent();
    }

    private static async Task<IResult> MarkAllReadAsync(
        ISender sender,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new MarkNotificationsReadCommand(NotificationAudience.Staff),
            cancellationToken);

        return result.IsError
            ? result.TopError.ToProblem(httpContext)
            : Results.NoContent();
    }
}
