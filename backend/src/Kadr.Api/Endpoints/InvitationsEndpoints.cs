using Kadr.Api.Http;
using Kadr.Api.Localization;
using Kadr.Platform.Application.Abstractions;
using Kadr.Platform.Application.Invitations;
using Kadr.SharedKernel.Constants;
using Kadr.SharedKernel.Results;
using Microsoft.AspNetCore.Mvc;

namespace Kadr.Api.Endpoints;

public static class InvitationsEndpoints
{
    public static IEndpointRouteBuilder MapInvitationsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/users/invite", InviteAsync)
            .RequireAuthorization(PermissionCodes.UsersInvite)
            .WithName("InviteUser");

        app.MapGet("/api/v1/invitations", ListPendingAsync)
            .RequireAuthorization(PermissionCodes.UsersRead)
            .WithName("ListPendingInvitations");

        app.MapPost("/api/v1/invitations/{id:guid}/resend", ResendAsync)
            .RequireAuthorization(PermissionCodes.UsersInvite)
            .WithName("ResendInvitation");

        app.MapPost("/api/v1/invitations/{id:guid}/revoke", RevokeAsync)
            .RequireAuthorization(PermissionCodes.UsersInvite)
            .WithName("RevokeInvitation");

        return app;
    }

    private static async Task<IResult> InviteAsync(
        [FromBody] InviteUserRequest request,
        HttpContext httpContext,
        IInvitationService invitations,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken)
    {
        var result = await invitations.InviteAsync(request, cancellationToken).ConfigureAwait(false);
        return ToResult(httpContext, result, localizer, StatusCodes.Status201Created);
    }

    private static async Task<IResult> ListPendingAsync(
        HttpContext httpContext,
        IInvitationService invitations,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken)
    {
        var result = await invitations.ListPendingAsync(cancellationToken).ConfigureAwait(false);
        return ToResult(httpContext, result, localizer);
    }

    private static async Task<IResult> ResendAsync(
        Guid id,
        [FromBody] ResendInvitationRequest? body,
        HttpContext httpContext,
        IInvitationService invitations,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken)
    {
        var result = await invitations
            .ResendAsync(id, body?.RoleId, cancellationToken)
            .ConfigureAwait(false);
        return ToResult(httpContext, result, localizer);
    }

    private static async Task<IResult> RevokeAsync(
        Guid id,
        HttpContext httpContext,
        IInvitationService invitations,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken)
    {
        var result = await invitations.RevokeAsync(id, cancellationToken).ConfigureAwait(false);
        if (result.IsSuccess)
        {
            return Results.NoContent();
        }

        return ToProblem(httpContext, result.ErrorCode!, localizer);
    }

    private static IResult ToResult<T>(
        HttpContext httpContext,
        Result<T> result,
        IErrorLocalizer localizer,
        int successStatus = StatusCodes.Status200OK)
    {
        if (result.IsSuccess)
        {
            return successStatus == StatusCodes.Status201Created
                ? Results.Created((string?)null, result.Value)
                : Results.Ok(result.Value);
        }

        return ToProblem(httpContext, result.ErrorCode ?? ErrorCodes.UsersValidationFailed, localizer);
    }

    private static IResult ToProblem(HttpContext httpContext, string code, IErrorLocalizer localizer)
    {
        var status = code switch
        {
            ErrorCodes.InvitationNotFound or ErrorCodes.RolesNotFound or ErrorCodes.TenantNotFound =>
                StatusCodes.Status404NotFound,
            ErrorCodes.AuthEmailTaken => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status400BadRequest,
        };
        return Results.Problem(ApiProblemDetails.Create(httpContext, status, code, localizer));
    }
}

public sealed record ResendInvitationRequest(Guid? RoleId);
