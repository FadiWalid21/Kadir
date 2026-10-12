using FluentValidation;
using Kadr.Api.Http;
using Kadr.Api.Localization;
using Kadr.Platform.Application.Abstractions;
using Kadr.Platform.Application.Payroll;
using Kadr.SharedKernel.Constants;
using Microsoft.AspNetCore.Mvc;

namespace Kadr.Api.Endpoints;

public static class EmployeeSalariesEndpoints
{
    public static IEndpointRouteBuilder MapEmployeeSalariesEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/employees/{employeeId:guid}/salaries")
            .WithTags("EmployeeSalaries");

        group.MapGet(string.Empty, ListAsync)
            .RequireAuthorization(PermissionCodes.SalaryRead)
            .WithName("ListEmployeeSalaries");

        group.MapGet("/current", GetCurrentAsync)
            .RequireAuthorization(PermissionCodes.SalaryRead)
            .WithName("GetCurrentEmployeeSalary");

        group.MapGet("/{id:guid}", GetAsync)
            .RequireAuthorization(PermissionCodes.SalaryRead)
            .WithName("GetEmployeeSalary");

        group.MapPost(string.Empty, CreateAsync)
            .RequireAuthorization(PermissionCodes.SalaryManage)
            .WithName("CreateEmployeeSalary");

        return app;
    }

    private static async Task<IResult> ListAsync(
        Guid employeeId,
        HttpContext httpContext,
        IEmployeeSalaryService salaries,
        IErrorLocalizer localizer,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var result = await salaries.ListAsync(employeeId, page, pageSize, cancellationToken)
            .ConfigureAwait(false);
        return ToResult(httpContext, result, localizer);
    }

    private static async Task<IResult> GetCurrentAsync(
        Guid employeeId,
        HttpContext httpContext,
        IEmployeeSalaryService salaries,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken)
    {
        var result = await salaries.GetCurrentAsync(employeeId, cancellationToken).ConfigureAwait(false);
        if (result.IsFailure)
        {
            return ToProblem(httpContext, result.ErrorCode, localizer);
        }

        // Always JSON body (including literal null) — Results.Ok(null) is 204.
        if (result.Value is null)
        {
            return Results.Text("null", "application/json");
        }

        return Results.Json(result.Value);
    }

    private static async Task<IResult> GetAsync(
        Guid employeeId,
        Guid id,
        HttpContext httpContext,
        IEmployeeSalaryService salaries,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken)
    {
        var result = await salaries.GetAsync(employeeId, id, cancellationToken).ConfigureAwait(false);
        return ToResult(httpContext, result, localizer);
    }

    private static async Task<IResult> CreateAsync(
        Guid employeeId,
        [FromBody] CreateEmployeeSalaryRequest request,
        HttpContext httpContext,
        IEmployeeSalaryService salaries,
        IValidator<CreateEmployeeSalaryRequest> validator,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken).ConfigureAwait(false);
        if (!validation.IsValid)
        {
            var code = validation.Errors
                .Select(e => e.ErrorCode)
                .FirstOrDefault(c => c is not null && c.StartsWith("salary.", StringComparison.Ordinal))
                ?? ErrorCodes.SalaryValidationFailed;
            var problem = ApiProblemDetails.Create(
                httpContext,
                StatusCodes.Status400BadRequest,
                code,
                localizer);
            problem.Extensions["errors"] = validation.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
            return Results.Problem(problem);
        }

        var result = await salaries.CreateAsync(employeeId, request, cancellationToken).ConfigureAwait(false);
        return ToResult(httpContext, result, localizer);
    }

    private static IResult ToResult<T>(
        HttpContext httpContext,
        SharedKernel.Results.Result<T> result,
        IErrorLocalizer localizer)
    {
        if (result.IsSuccess)
        {
            return Results.Ok(result.Value);
        }

        return ToProblem(httpContext, result.ErrorCode, localizer);
    }

    private static IResult ToProblem(HttpContext httpContext, string? errorCode, IErrorLocalizer localizer)
    {
        var code = errorCode ?? ErrorCodes.SalaryValidationFailed;
        var status = code switch
        {
            ErrorCodes.SalaryNotFound or ErrorCodes.SalaryEmployeeNotFound => StatusCodes.Status404NotFound,
            ErrorCodes.SalaryEffectiveOverlap or ErrorCodes.SalaryInvalidComponent => StatusCodes.Status400BadRequest,
            _ => StatusCodes.Status400BadRequest,
        };
        return Results.Problem(ApiProblemDetails.Create(httpContext, status, code, localizer));
    }
}
