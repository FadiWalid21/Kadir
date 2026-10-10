using Kadr.Api.Localization;
using Microsoft.AspNetCore.Mvc;

namespace Kadr.Api.Http;

public static class ApiProblemDetails
{
    public static ProblemDetails Create(
        HttpContext httpContext,
        int statusCode,
        string errorCode,
        IErrorLocalizer localizer,
        string? title = null)
    {
        var acceptLanguage = httpContext.Request.Headers.AcceptLanguage.ToString();
        return new ProblemDetails
        {
            Status = statusCode,
            Title = title ?? ReasonPhrases.GetReasonPhrase(statusCode),
            Detail = localizer.GetDetail(errorCode, acceptLanguage),
            Instance = httpContext.Request.Path,
            Extensions = { ["code"] = errorCode },
        };
    }
}

internal static class ReasonPhrases
{
    public static string GetReasonPhrase(int statusCode) => statusCode switch
    {
        400 => "Bad Request",
        409 => "Conflict",
        429 => "Too Many Requests",
        _ => "Error",
    };
}
