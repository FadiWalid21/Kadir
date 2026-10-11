using FluentValidation;
using Kadr.Api.Http;
using Kadr.Api.Localization;
using Kadr.SharedKernel.Constants;
using Kadr.SharedKernel.Results;

namespace Kadr.Api.Endpoints;

/// <summary>Shared validation and ProblemDetails mapping for the platform admin endpoints.</summary>
internal static class PlatformEndpointResults
{
    public static async Task<IResult?> ValidateAsync<T>(
        T request,
        IValidator<T> validator,
        HttpContext httpContext,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return ToProblem(httpContext, ErrorCodes.PlatformValidationFailed, localizer);
        }

        var validation = await validator.ValidateAsync(request, cancellationToken).ConfigureAwait(false);
        if (validation.IsValid)
        {
            return null;
        }

        var code = validation.Errors
            .Select(e => e.ErrorCode)
            .FirstOrDefault(c => c is not null && c.StartsWith("platform.", StringComparison.Ordinal))
            ?? ErrorCodes.PlatformValidationFailed;
        return ToProblem(httpContext, code, localizer);
    }

    public static IResult ToResult<T>(HttpContext httpContext, Result<T> result, IErrorLocalizer localizer) =>
        result.IsSuccess
            ? Results.Ok(result.Value)
            : ToProblem(httpContext, result.ErrorCode, localizer);

    public static IResult ToProblem(HttpContext httpContext, string? code, IErrorLocalizer localizer)
    {
        var errorCode = code ?? ErrorCodes.PlatformValidationFailed;
        var status = errorCode switch
        {
            ErrorCodes.PlatformPlanNotFound => StatusCodes.Status404NotFound,
            ErrorCodes.PlatformPlanProtected => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status400BadRequest,
        };
        return Results.Problem(ApiProblemDetails.Create(httpContext, status, errorCode, localizer));
    }
}
