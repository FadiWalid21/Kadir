using Kadr.Api.Http;
using Kadr.Api.Localization;
using Kadr.Platform.Application.Abstractions;
using Kadr.Platform.Application.Roles;
using Kadr.SharedKernel.Constants;
using Kadr.SharedKernel.Results;
using Microsoft.AspNetCore.Mvc;

namespace Kadr.Api.Endpoints;

public static class RolesEndpoints
{
    public static IEndpointRouteBuilder MapRolesEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/roles", ListAsync)
            .RequireAuthorization(PermissionCodes.RolesRead)
            .WithName("ListRoles");

        app.MapGet("/api/v1/roles/{id:guid}", GetAsync)
            .RequireAuthorization(PermissionCodes.RolesRead)
            .WithName("GetRole");

        app.MapPost("/api/v1/roles", CreateAsync)
            .RequireAuthorization(PermissionCodes.RolesManage)
            .WithName("CreateRole");

        app.MapPut("/api/v1/roles/{id:guid}", UpdateAsync)
            .RequireAuthorization(PermissionCodes.RolesManage)
            .WithName("UpdateRole");

        app.MapDelete("/api/v1/roles/{id:guid}", DeleteAsync)
            .RequireAuthorization(PermissionCodes.RolesManage)
            .WithName("DeleteRole");

        app.MapGet("/api/v1/permissions", ListPermissionsAsync)
            .RequireAuthorization(PermissionCodes.RolesRead)
            .WithName("ListPermissions");

        return app;
    }

    private static async Task<IResult> ListAsync(
        HttpContext httpContext,
        IRoleService roles,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken)
    {
        var result = await roles.ListAsync(cancellationToken).ConfigureAwait(false);
        return ToResult(httpContext, result, localizer);
    }

    private static async Task<IResult> GetAsync(
        Guid id,
        HttpContext httpContext,
        IRoleService roles,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken)
    {
        var result = await roles.GetAsync(id, cancellationToken).ConfigureAwait(false);
        return ToResult(httpContext, result, localizer);
    }

    private static async Task<IResult> CreateAsync(
        [FromBody] CreateRoleRequest request,
        HttpContext httpContext,
        IRoleService roles,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken)
    {
        var result = await roles.CreateAsync(request, cancellationToken).ConfigureAwait(false);
        return ToResult(httpContext, result, localizer, StatusCodes.Status201Created);
    }

    private static async Task<IResult> UpdateAsync(
        Guid id,
        [FromBody] UpdateRoleRequest request,
        HttpContext httpContext,
        IRoleService roles,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken)
    {
        var result = await roles.UpdateAsync(id, request, cancellationToken).ConfigureAwait(false);
        return ToResult(httpContext, result, localizer);
    }

    private static async Task<IResult> DeleteAsync(
        Guid id,
        HttpContext httpContext,
        IRoleService roles,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken)
    {
        var result = await roles.DeleteAsync(id, cancellationToken).ConfigureAwait(false);
        if (result.IsSuccess)
        {
            return Results.NoContent();
        }

        return ToProblem(httpContext, result.ErrorCode!, localizer);
    }

    private static async Task<IResult> ListPermissionsAsync(
        HttpContext httpContext,
        IRoleService roles,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken)
    {
        var result = await roles.ListPermissionsAsync(cancellationToken).ConfigureAwait(false);
        return ToResult(httpContext, result, localizer);
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

        return ToProblem(httpContext, result.ErrorCode ?? ErrorCodes.RolesValidationFailed, localizer);
    }

    private static IResult ToProblem(HttpContext httpContext, string code, IErrorLocalizer localizer)
    {
        var status = code switch
        {
            ErrorCodes.RolesNotFound or ErrorCodes.TenantNotFound => StatusCodes.Status404NotFound,
            ErrorCodes.RolesSystemReadonly or ErrorCodes.RolesInUse
                or ErrorCodes.RolesPrivilegeEscalation => StatusCodes.Status400BadRequest,
            _ => StatusCodes.Status400BadRequest,
        };
        return Results.Problem(ApiProblemDetails.Create(httpContext, status, code, localizer));
    }
}
