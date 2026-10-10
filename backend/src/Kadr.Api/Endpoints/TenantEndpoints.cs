using FluentValidation;
using Kadr.Api.Http;
using Kadr.Api.Localization;
using Kadr.Infrastructure.Persistence;
using Kadr.Platform.Application.Tenants;
using Kadr.SharedKernel.Abstractions;
using Kadr.SharedKernel.Application;
using Kadr.SharedKernel.Constants;
using Kadr.SharedKernel.Tenancy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Kadr.Api.Endpoints;

public static class TenantEndpoints
{
    public static IEndpointRouteBuilder MapTenantEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/tenant/settings", GetSettingsAsync)
            .RequireAuthorization(PermissionCodes.TenantSettingsRead)
            .WithName("GetTenantSettings");

        app.MapPut("/api/v1/tenant/settings", UpdateSettingsAsync)
            .RequireAuthorization(PermissionCodes.TenantSettingsUpdate)
            .WithName("UpdateTenantSettings");

        app.MapPut("/api/v1/tenant/logo", UploadLogoAsync)
            .RequireAuthorization(PermissionCodes.TenantSettingsUpdate)
            .DisableAntiforgery()
            .WithName("UploadTenantLogo");

        app.MapDelete("/api/v1/tenant/logo", DeleteLogoAsync)
            .RequireAuthorization(PermissionCodes.TenantSettingsUpdate)
            .WithName("DeleteTenantLogo");

        app.MapGet("/api/v1/files/{fileId:guid}", GetFileAsync)
            .RequireAuthorization()
            .WithName("GetTenantFile");

        return app;
    }

    private static async Task<IResult> GetSettingsAsync(
        ITenantContext tenantContext,
        KadrDbContext db,
        CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is null)
        {
            return Results.Unauthorized();
        }

        var tenant = await db.Tenants.AsNoTracking()
            .Where(t => t.Id == tenantContext.TenantId.Value)
            .Select(t => new { t.Name, t.DefaultLanguage, t.TimeZone, t.LogoFileId })
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (tenant is null)
        {
            return Results.NotFound();
        }

        var lang = tenant.DefaultLanguage == Platform.Domain.Tenants.TenantLanguage.En ? "en" : "ar";
        return Results.Ok(new TenantSettingsResponse(
            tenant.Name,
            lang,
            tenant.TimeZone,
            tenant.LogoFileId,
            tenant.LogoFileId is null ? null : $"/api/v1/files/{tenant.LogoFileId:D}"));
    }

    private static async Task<IResult> UpdateSettingsAsync(
        [FromBody] UpdateTenantSettingsRequest request,
        HttpContext httpContext,
        ICommandDispatcher dispatcher,
        IValidator<UpdateTenantSettingsCommand> validator,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken)
    {
        var command = new UpdateTenantSettingsCommand(request.Name, request.DefaultLanguage, request.TimeZone);
        var validation = await validator.ValidateAsync(command, cancellationToken).ConfigureAwait(false);
        if (!validation.IsValid)
        {
            var problem = ApiProblemDetails.Create(
                httpContext,
                StatusCodes.Status400BadRequest,
                ErrorCodes.TenantSettingsValidationFailed,
                localizer);
            problem.Extensions["errors"] = validation.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
            return Results.Problem(problem);
        }

        var result = await dispatcher.SendAsync(command, cancellationToken).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            return Results.Problem(ApiProblemDetails.Create(
                httpContext,
                StatusCodes.Status400BadRequest,
                result.ErrorCode ?? ErrorCodes.TenantSettingsValidationFailed,
                localizer));
        }

        var value = result.Value!;
        return Results.Ok(new TenantSettingsResponse(
            value.Name,
            value.DefaultLanguage,
            value.TimeZone,
            value.LogoFileId,
            value.LogoUrl));
    }

    private static async Task<IResult> UploadLogoAsync(
        HttpRequest request,
        HttpContext httpContext,
        ICommandDispatcher dispatcher,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken)
    {
        if (!request.HasFormContentType)
        {
            return Results.Problem(ApiProblemDetails.Create(
                httpContext,
                StatusCodes.Status400BadRequest,
                ErrorCodes.FileInvalidType,
                localizer));
        }

        var form = await request.ReadFormAsync(cancellationToken).ConfigureAwait(false);
        var file = form.Files.GetFile("file") ?? form.Files.FirstOrDefault();
        if (file is null || file.Length == 0)
        {
            return Results.Problem(ApiProblemDetails.Create(
                httpContext,
                StatusCodes.Status400BadRequest,
                ErrorCodes.FileInvalidType,
                localizer));
        }

        await using var stream = file.OpenReadStream();
        var result = await dispatcher
            .SendAsync(new UploadTenantLogoCommand(stream, file.ContentType, file.FileName), cancellationToken)
            .ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            var code = result.ErrorCode ?? ErrorCodes.FileInvalidType;
            var status = code == ErrorCodes.FileTooLarge
                ? StatusCodes.Status413PayloadTooLarge
                : StatusCodes.Status400BadRequest;
            return Results.Problem(ApiProblemDetails.Create(httpContext, status, code, localizer));
        }

        return Results.Ok(new TenantLogoResponse(result.Value!.LogoFileId, result.Value.LogoUrl));
    }

    private static async Task<IResult> DeleteLogoAsync(
        HttpContext httpContext,
        ICommandDispatcher dispatcher,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken)
    {
        var result = await dispatcher.SendAsync(new DeleteTenantLogoCommand(), cancellationToken)
            .ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            return Results.Problem(ApiProblemDetails.Create(
                httpContext,
                StatusCodes.Status400BadRequest,
                result.ErrorCode ?? ErrorCodes.FileNotFound,
                localizer));
        }

        return Results.NoContent();
    }

    private static async Task<IResult> GetFileAsync(
        Guid fileId,
        ITenantContext tenantContext,
        KadrDbContext db,
        IFileStorage fileStorage,
        CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is null)
        {
            return Results.Unauthorized();
        }

        var file = await db.FileObjects.AsNoTracking()
            .Where(f => f.Id == fileId)
            .Select(f => new { f.StoragePath, f.ContentType, f.FileName })
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (file is null)
        {
            return Results.NotFound();
        }

        try
        {
            var stream = await fileStorage.OpenReadAsync(file.StoragePath, cancellationToken).ConfigureAwait(false);
            return Results.File(stream, file.ContentType, file.FileName, enableRangeProcessing: false);
        }
        catch (FileNotFoundException)
        {
            return Results.NotFound();
        }
    }
}

public sealed record UpdateTenantSettingsRequest(string Name, string? DefaultLanguage, string? TimeZone);

public sealed record TenantSettingsResponse(
    string Name,
    string DefaultLanguage,
    string TimeZone,
    Guid? LogoFileId,
    string? LogoUrl);

public sealed record TenantLogoResponse(Guid LogoFileId, string LogoUrl);
