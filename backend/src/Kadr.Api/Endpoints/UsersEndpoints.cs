using Kadr.Api.Http;
using Kadr.Api.Localization;
using Kadr.Platform.Application.Abstractions;
using Kadr.Platform.Application.Users;
using Kadr.SharedKernel.Constants;
using Kadr.SharedKernel.Results;
using Microsoft.AspNetCore.Mvc;

namespace Kadr.Api.Endpoints;

public static class UsersEndpoints
{
    public static IEndpointRouteBuilder MapUsersEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/users", ListAsync)
            .RequireAuthorization(PermissionCodes.UsersRead)
            .WithName("ListUsers");

        app.MapGet("/api/v1/users/{id:guid}", GetAsync)
            .RequireAuthorization(PermissionCodes.UsersRead)
            .WithName("GetUser");

        app.MapPut("/api/v1/users/{id:guid}", UpdateAsync)
            .RequireAuthorization(PermissionCodes.UsersUpdate)
            .WithName("UpdateUser");

        app.MapPut("/api/v1/users/{id:guid}/employee", SetEmployeeAsync)
            .RequireAuthorization(PermissionCodes.UsersUpdate)
            .WithName("SetUserEmployee");

        return app;
    }

    private static async Task<IResult> ListAsync(
        [FromQuery] int page,
        [FromQuery] int pageSize,
        [FromQuery] string? q,
        [FromQuery] Guid? roleId,
        [FromQuery] bool? active,
        HttpContext httpContext,
        IUserService users,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken)
    {
        var result = await users
            .ListAsync(page, pageSize == 0 ? 20 : pageSize, q, roleId, active, cancellationToken)
            .ConfigureAwait(false);
        return ToResult(httpContext, result, localizer);
    }

    private static async Task<IResult> GetAsync(
        Guid id,
        HttpContext httpContext,
        IUserService users,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken)
    {
        var result = await users.GetAsync(id, cancellationToken).ConfigureAwait(false);
        return ToResult(httpContext, result, localizer);
    }

    private static async Task<IResult> UpdateAsync(
        Guid id,
        [FromBody] UpdateUserRequest request,
        HttpContext httpContext,
        IUserService users,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken)
    {
        var result = await users.UpdateAsync(id, request, cancellationToken).ConfigureAwait(false);
        return ToResult(httpContext, result, localizer);
    }

    private static async Task<IResult> SetEmployeeAsync(
        Guid id,
        [FromBody] SetUserEmployeeRequest request,
        HttpContext httpContext,
        IUserService users,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken)
    {
        var result = await users.SetEmployeeLinkAsync(id, request, cancellationToken).ConfigureAwait(false);
        return ToResult(httpContext, result, localizer);
    }

    private static IResult ToResult<T>(
        HttpContext httpContext,
        Result<T> result,
        IErrorLocalizer localizer)
    {
        if (result.IsSuccess)
        {
            return Results.Ok(result.Value);
        }

        return ToProblem(httpContext, result.ErrorCode ?? ErrorCodes.UsersValidationFailed, localizer);
    }

    private static IResult ToProblem(HttpContext httpContext, string code, IErrorLocalizer localizer)
    {
        var status = code switch
        {
            ErrorCodes.UsersNotFound
                or ErrorCodes.TenantNotFound
                or ErrorCodes.RolesNotFound
                or ErrorCodes.EmployeesNotFound =>
                StatusCodes.Status404NotFound,
            ErrorCodes.UsersPrivilegeEscalation
                or ErrorCodes.UsersLastOwnerProtected
                or ErrorCodes.UsersSelfDeactivationForbidden
                or ErrorCodes.UsersEmployeeAlreadyLinked
                or ErrorCodes.UsersEmployeeLinkConflict
                or ErrorCodes.UsersEmployeeInactive => StatusCodes.Status400BadRequest,
            _ => StatusCodes.Status400BadRequest,
        };
        return Results.Problem(ApiProblemDetails.Create(httpContext, status, code, localizer));
    }
}
