using Kadr.Api.Http;
using Kadr.Api.Localization;
using Kadr.Platform.Application.Abstractions;
using Kadr.SharedKernel.Constants;

namespace Kadr.Api.Endpoints;

public static class OvertimeMultipliersEndpoints
{
    public static IEndpointRouteBuilder MapOvertimeMultipliersEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/payroll/overtime-multipliers", GetAsync)
            .RequireAuthorization(PermissionCodes.SalaryRead)
            .WithName("GetOvertimeMultipliers")
            .WithTags("Payroll");

        return app;
    }

    private static async Task<IResult> GetAsync(
        HttpContext httpContext,
        IOvertimeMultipliersService service,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken)
    {
        var result = await service.GetAsync(cancellationToken).ConfigureAwait(false);
        if (result.IsSuccess)
        {
            return Results.Ok(result.Value);
        }

        return Results.Problem(
            ApiProblemDetails.Create(
                httpContext,
                StatusCodes.Status400BadRequest,
                result.ErrorCode ?? ErrorCodes.SalaryValidationFailed,
                localizer));
    }
}
