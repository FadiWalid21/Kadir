using FluentValidation;
using Kadr.Api.Http;
using Kadr.Api.Localization;
using Kadr.Platform.Application.Abstractions;
using Kadr.Platform.Application.Payroll;
using Kadr.SharedKernel.Constants;
using Microsoft.AspNetCore.Mvc;

namespace Kadr.Api.Endpoints;

public static class EmployeeInsuranceEndpoints
{
    public static IEndpointRouteBuilder MapEmployeeInsuranceEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/employees/{employeeId:guid}/insurance")
            .WithTags("EmployeeInsurance");

        group.MapGet(string.Empty, GetAsync)
            .RequireAuthorization(PermissionCodes.SalaryRead)
            .WithName("GetEmployeeInsurance");

        group.MapPut(string.Empty, UpsertAsync)
            .RequireAuthorization(PermissionCodes.SalaryManage)
            .WithName("UpsertEmployeeInsurance");

        return app;
    }

    private static async Task<IResult> GetAsync(
        Guid employeeId,
        HttpContext httpContext,
        IEmployeeInsuranceService insurance,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken)
    {
        var result = await insurance.GetAsync(employeeId, cancellationToken).ConfigureAwait(false);
        if (result.IsFailure)
        {
            return ToProblem(httpContext, result.ErrorCode, localizer);
        }

        if (result.Value is null)
        {
            return Results.Text("null", "application/json");
        }

        return Results.Json(result.Value);
    }

    private static async Task<IResult> UpsertAsync(
        Guid employeeId,
        [FromBody] UpsertEmployeeInsuranceRequest request,
        HttpContext httpContext,
        IEmployeeInsuranceService insurance,
        IValidator<UpsertEmployeeInsuranceRequest> validator,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken).ConfigureAwait(false);
        if (!validation.IsValid)
        {
            var code = validation.Errors
                .Select(e => e.ErrorCode)
                .FirstOrDefault(c => c is not null && c.StartsWith("insurance.", StringComparison.Ordinal))
                ?? ErrorCodes.InsuranceValidationFailed;
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

        var result = await insurance.UpsertAsync(employeeId, request, cancellationToken)
            .ConfigureAwait(false);
        if (result.IsSuccess)
        {
            return Results.Ok(result.Value);
        }

        return ToProblem(httpContext, result.ErrorCode, localizer);
    }

    private static IResult ToProblem(HttpContext httpContext, string? errorCode, IErrorLocalizer localizer)
    {
        var code = errorCode ?? ErrorCodes.InsuranceValidationFailed;
        var status = code switch
        {
            ErrorCodes.InsuranceNotFound or ErrorCodes.InsuranceEmployeeNotFound => StatusCodes.Status404NotFound,
            _ => StatusCodes.Status400BadRequest,
        };
        return Results.Problem(ApiProblemDetails.Create(httpContext, status, code, localizer));
    }
}
