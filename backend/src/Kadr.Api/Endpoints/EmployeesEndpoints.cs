using Kadr.Api.Http;
using Kadr.Api.Localization;
using Kadr.Platform.Application.Abstractions;
using Kadr.Platform.Application.Employees;
using Kadr.Platform.Domain.Employees;
using Kadr.SharedKernel.Constants;
using Kadr.SharedKernel.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kadr.Api.Endpoints;

public static class EmployeesEndpoints
{
    public static IEndpointRouteBuilder MapEmployeesEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/employees", ListAsync)
            .RequireAuthorization(PermissionCodes.EmployeesRead)
            .WithName("ListEmployees");

        app.MapGet("/api/v1/employees/{id:guid}", GetAsync)
            .RequireAuthorization(PermissionCodes.EmployeesRead)
            .WithName("GetEmployee");

        app.MapPost("/api/v1/employees", CreateAsync)
            .RequireAuthorization(PermissionCodes.EmployeesCreate)
            .WithName("CreateEmployee");

        app.MapPut("/api/v1/employees/{id:guid}", UpdateAsync)
            .RequireAuthorization(PermissionCodes.EmployeesUpdate)
            .WithName("UpdateEmployee");

        app.MapPost("/api/v1/employees/{id:guid}/status", ChangeStatusAsync)
            .RequireAuthorization(PermissionCodes.EmployeesUpdate)
            .WithName("ChangeEmployeeStatus");

        app.MapGet("/api/v1/employees/{id:guid}/documents", ListDocumentsAsync)
            .RequireAuthorization(PermissionCodes.EmployeesRead)
            .WithName("ListEmployeeDocuments");

        app.MapPost("/api/v1/employees/{id:guid}/documents", UploadDocumentAsync)
            .RequireAuthorization(PermissionCodes.EmployeesDocumentsManage)
            .DisableAntiforgery()
            .WithName("UploadEmployeeDocument");

        app.MapDelete("/api/v1/employees/{employeeId:guid}/documents/{documentId:guid}", DeleteDocumentAsync)
            .RequireAuthorization(PermissionCodes.EmployeesDocumentsManage)
            .WithName("DeleteEmployeeDocument");

        app.MapGet("/api/v1/employees/{employeeId:guid}/documents/{documentId:guid}/download", DownloadDocumentAsync)
            .RequireAuthorization(PermissionCodes.EmployeesRead)
            .WithName("DownloadEmployeeDocument");

        app.MapGet("/api/v1/documents/expiring", ListExpiringAsync)
            .RequireAuthorization(PermissionCodes.EmployeesRead)
            .WithName("ListExpiringDocuments");

        app.MapGet("/api/v1/org/tree", GetOrgTreeAsync)
            .RequireAuthorization(PermissionCodes.OrgRead)
            .WithName("GetOrgTree");

        app.MapGet("/api/v1/org/lookups", GetOrgLookupsAsync)
            .RequireAuthorization(PermissionCodes.EmployeesRead)
            .WithName("GetOrgLookups");

        app.MapPatch("/api/v1/org/departments/{id:guid}", UpdateDepartmentAsync)
            .RequireAuthorization(PermissionCodes.OrgManage)
            .WithName("UpdateOrgDepartment");

        app.MapGet("/api/v1/employees/import/template", GetImportTemplateAsync)
            .RequireAuthorization(PermissionCodes.EmployeesImport)
            .WithName("GetEmployeeImportTemplate");

        app.MapPost("/api/v1/employees/import/preview", PreviewImportAsync)
            .RequireAuthorization(PermissionCodes.EmployeesImport)
            .DisableAntiforgery()
            .WithName("PreviewEmployeeImport");

        app.MapPost("/api/v1/employees/import/commit", CommitImportAsync)
            .RequireAuthorization(PermissionCodes.EmployeesImport)
            .WithName("CommitEmployeeImport");

        return app;
    }

    private static async Task<IResult> ListAsync(
        HttpContext httpContext,
        IEmployeeService employees,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? q = null,
        [FromQuery] string? status = null,
        [FromQuery] Guid? departmentId = null,
        [FromQuery] Guid? branchId = null,
        [FromQuery] Guid? jobTitleId = null,
        [FromQuery] Guid? managerId = null)
    {
        var result = await employees
            .ListAsync(page, pageSize == 0 ? 20 : pageSize, q, status, departmentId, branchId, jobTitleId, managerId, cancellationToken)
            .ConfigureAwait(false);
        return ToResult(httpContext, result, localizer);
    }

    private static async Task<IResult> GetAsync(
        Guid id,
        HttpContext httpContext,
        IEmployeeService employees,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken)
    {
        return ToResult(httpContext, await employees.GetAsync(id, cancellationToken).ConfigureAwait(false), localizer);
    }

    private static async Task<IResult> CreateAsync(
        [FromBody] CreateEmployeeRequest request,
        HttpContext httpContext,
        IEmployeeService employees,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken)
    {
        return ToResult(httpContext, await employees.CreateAsync(request, cancellationToken).ConfigureAwait(false), localizer);
    }

    private static async Task<IResult> UpdateAsync(
        Guid id,
        [FromBody] UpdateEmployeeRequest request,
        HttpContext httpContext,
        IEmployeeService employees,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken)
    {
        return ToResult(
            httpContext,
            await employees.UpdateAsync(id, request, cancellationToken).ConfigureAwait(false),
            localizer);
    }

    private static async Task<IResult> ChangeStatusAsync(
        Guid id,
        [FromBody] ChangeEmployeeStatusRequest request,
        HttpContext httpContext,
        IEmployeeService employees,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken)
    {
        if (Enum.TryParse<EmployeeStatus>(request.Status, true, out var st)
            && st is EmployeeStatus.Terminated or EmployeeStatus.Suspended)
        {
            var auth = httpContext.RequestServices.GetRequiredService<IAuthorizationService>();
            var allowed = await auth
                .AuthorizeAsync(httpContext.User, resource: null, policyName: PermissionCodes.EmployeesTerminate)
                .ConfigureAwait(false);
            if (!allowed.Succeeded)
            {
                return Results.Problem(
                    ApiProblemDetails.Create(
                        httpContext,
                        StatusCodes.Status403Forbidden,
                        ErrorCodes.EmployeesForbidden,
                        localizer));
            }
        }

        return ToResult(
            httpContext,
            await employees.ChangeStatusAsync(id, request, cancellationToken).ConfigureAwait(false),
            localizer);
    }

    private static async Task<IResult> ListDocumentsAsync(
        Guid id,
        HttpContext httpContext,
        IEmployeeService employees,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken)
    {
        return ToResult(
            httpContext,
            await employees.ListDocumentsAsync(id, cancellationToken).ConfigureAwait(false),
            localizer);
    }

    private static async Task<IResult> UploadDocumentAsync(
        Guid id,
        [FromForm] Guid documentTypeId,
        [FromForm] string? referenceNumber,
        [FromForm] DateOnly? issuedOn,
        [FromForm] DateOnly? expiresOn,
        IFormFile file,
        HttpContext httpContext,
        IEmployeeService employees,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken)
    {
        if (file.Length == 0)
        {
            return Results.Problem(
                ApiProblemDetails.Create(
                    httpContext,
                    StatusCodes.Status400BadRequest,
                    ErrorCodes.EmployeesValidationFailed,
                    localizer));
        }

        await using var stream = file.OpenReadStream();
        var result = await employees
            .UploadDocumentAsync(
                id,
                new UploadEmployeeDocumentRequest(documentTypeId, referenceNumber, issuedOn, expiresOn),
                stream,
                file.FileName,
                file.ContentType,
                cancellationToken)
            .ConfigureAwait(false);
        return ToResult(httpContext, result, localizer);
    }

    private static async Task<IResult> DeleteDocumentAsync(
        Guid employeeId,
        Guid documentId,
        HttpContext httpContext,
        IEmployeeService employees,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken)
    {
        var result = await employees.DeleteDocumentAsync(employeeId, documentId, cancellationToken)
            .ConfigureAwait(false);
        if (result.IsSuccess)
        {
            return Results.NoContent();
        }

        return ToProblem(httpContext, result.ErrorCode ?? ErrorCodes.EmployeesValidationFailed, localizer);
    }

    private static async Task<IResult> DownloadDocumentAsync(
        Guid employeeId,
        Guid documentId,
        HttpContext httpContext,
        IEmployeeService employees,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken)
    {
        var result = await employees.OpenDocumentAsync(employeeId, documentId, cancellationToken)
            .ConfigureAwait(false);
        if (result.IsFailure)
        {
            return ToProblem(httpContext, result.ErrorCode ?? ErrorCodes.FileNotFound, localizer);
        }

        var (stream, fileName, contentType) = result.Value!;
        return Results.File(stream, contentType, fileName);
    }

    private static async Task<IResult> ListExpiringAsync(
        HttpContext httpContext,
        IEmployeeService employees,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken,
        [FromQuery] string? window = null,
        [FromQuery] string? q = null,
        [FromQuery] Guid? documentTypeId = null,
        [FromQuery] Guid? departmentId = null,
        [FromQuery] Guid? branchId = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await employees
            .ListExpiringAsync(
                window ?? "all",
                q,
                documentTypeId,
                departmentId,
                branchId,
                page,
                pageSize == 0 ? 20 : pageSize,
                cancellationToken)
            .ConfigureAwait(false);
        return ToResult(httpContext, result, localizer);
    }

    private static async Task<IResult> GetOrgTreeAsync(
        HttpContext httpContext,
        IOrgService org,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken)
    {
        return ToResult(httpContext, await org.GetTreeAsync(cancellationToken).ConfigureAwait(false), localizer);
    }

    private static async Task<IResult> GetOrgLookupsAsync(
        HttpContext httpContext,
        IOrgService org,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken)
    {
        return ToResult(httpContext, await org.GetLookupsAsync(cancellationToken).ConfigureAwait(false), localizer);
    }

    private static async Task<IResult> UpdateDepartmentAsync(
        Guid id,
        [FromBody] UpdateDepartmentHierarchyRequest request,
        HttpContext httpContext,
        IOrgService org,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken)
    {
        var result = await org.UpdateDepartmentAsync(id, request, cancellationToken).ConfigureAwait(false);
        if (result.IsSuccess)
        {
            return Results.NoContent();
        }

        return ToProblem(httpContext, result.ErrorCode ?? ErrorCodes.OrgNotFound, localizer);
    }

    private static async Task<IResult> GetImportTemplateAsync(
        IEmployeeImportService import,
        CancellationToken cancellationToken)
    {
        var result = await import.GetTemplateAsync(cancellationToken).ConfigureAwait(false);
        return Results.File(result.Value!, "text/csv", "employees-import-template.csv");
    }

    private static async Task<IResult> PreviewImportAsync(
        IFormFile file,
        HttpContext httpContext,
        IEmployeeImportService import,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken)
    {
        await using var stream = file.OpenReadStream();
        var result = await import.PreviewAsync(stream, file.FileName, cancellationToken).ConfigureAwait(false);
        return ToResult(httpContext, result, localizer);
    }

    private static async Task<IResult> CommitImportAsync(
        [FromBody] IReadOnlyList<CreateEmployeeRequest> rows,
        HttpContext httpContext,
        IEmployeeImportService import,
        IErrorLocalizer localizer,
        CancellationToken cancellationToken)
    {
        return ToResult(
            httpContext,
            await import.CommitAsync(rows, cancellationToken).ConfigureAwait(false),
            localizer);
    }

    private static IResult ToResult<T>(HttpContext httpContext, Result<T> result, IErrorLocalizer localizer)
    {
        if (result.IsSuccess)
        {
            return Results.Ok(result.Value);
        }

        return ToProblem(httpContext, result.ErrorCode ?? ErrorCodes.EmployeesValidationFailed, localizer);
    }

    private static IResult ToProblem(HttpContext httpContext, string code, IErrorLocalizer localizer)
    {
        var status = code switch
        {
            ErrorCodes.EmployeesNotFound or ErrorCodes.OrgNotFound or ErrorCodes.FileNotFound =>
                StatusCodes.Status404NotFound,
            ErrorCodes.PlanFeatureNotAvailable or ErrorCodes.EmployeesForbidden => StatusCodes.Status403Forbidden,
            ErrorCodes.PlanEmployeeCapExceeded => StatusCodes.Status409Conflict,
            ErrorCodes.SubscriptionExpired => StatusCodes.Status403Forbidden,
            _ => StatusCodes.Status400BadRequest,
        };
        return Results.Problem(ApiProblemDetails.Create(httpContext, status, code, localizer));
    }
}
