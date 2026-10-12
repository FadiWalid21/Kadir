using System.Globalization;
using System.Text.Json;
using Kadr.Platform.Application.Abstractions;
using Kadr.Platform.Application.Reports;
using Kadr.Platform.Domain.Attendance;
using Kadr.Platform.Domain.Employees;
using Kadr.Infrastructure.Persistence.Employees;
using Kadr.SharedKernel.Constants;
using Kadr.SharedKernel.Results;
using Kadr.SharedKernel.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace Kadr.Infrastructure.Persistence.Reports;

internal sealed class HeadcountReportService(
    KadrDbContext db,
    ITenantContext tenantContext,
    ISubscriptionGuard subscriptionGuard,
    IReportExportService exportService,
    IAuditLogWriter audit) : IHeadcountReportService
{
    private static readonly HashSet<string> SortWhitelist = new(StringComparer.OrdinalIgnoreCase)
    {
        "employeeNumber",
        "fullNameAr",
        "fullNameEn",
        "hireDate",
        "status",
        "contractType",
        "branch",
        "department",
    };

    public Task<Result<IReadOnlyList<ReportHubItemDto>>> ListHubAsync(
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<ReportHubItemDto> items =
        [
            new("headcount", "reports.hub.headcount", "reports.hub.group.people", "/reports/headcount", true),
            new("attendance", "reports.hub.attendance", "reports.hub.group.time", "/reports/attendance", false),
            new("leave", "reports.hub.leave", "reports.hub.group.time", "/reports/leave", false),
            new("documents", "reports.hub.documents", "reports.hub.group.documents", "/reports/documents", false),
            new("turnover", "reports.hub.turnover", "reports.hub.group.people", "/reports/turnover", false),
        ];
        return Task.FromResult(Result.Success(items));
    }

    public async Task<Result<HeadcountReportDto>> GetHeadcountAsync(
        HeadcountReportQuery query,
        CancellationToken cancellationToken = default)
    {
        var gate = await subscriptionGuard.EnsureFeatureAsync(FeatureKeys.Reports, cancellationToken)
            .ConfigureAwait(false);
        if (gate.IsFailure)
        {
            return Result.Failure<HeadcountReportDto>(gate.ErrorCode!, gate.ErrorMessage!);
        }

        if (tenantContext.TenantId is null || tenantContext.UserId is null)
        {
            return Result.Failure<HeadcountReportDto>(ErrorCodes.TenantNotFound, ErrorCodes.TenantNotFound);
        }

        var prepared = await PrepareQueryAsync(query, cancellationToken).ConfigureAwait(false);
        if (prepared.IsFailure)
        {
            return Result.Failure<HeadcountReportDto>(prepared.ErrorCode!, prepared.ErrorMessage!);
        }

        var (asOf, scoped, page, pageSize, sortBy, sortDesc) = prepared.Value!;

        var statusCounts = await scoped
            .GroupBy(e => e.Status)
            .Select(g => new HeadcountStatusCountDto(g.Key.ToString(), g.Count()))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var total = statusCounts.Sum(x => x.Count);
        var summary = new HeadcountSummaryDto(total, statusCounts);

        var ordered = ApplySort(scoped, sortBy, sortDesc);
        var rows = await ProjectRows(ordered)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return Result.Success(new HeadcountReportDto(asOf, summary, rows, page, pageSize, total));
    }

    public async Task<Result<ReportExportFileDto>> ExportHeadcountAsync(
        HeadcountReportQuery query,
        string format,
        string? acceptLanguage,
        CancellationToken cancellationToken = default)
    {
        var gate = await subscriptionGuard.EnsureFeatureAsync(FeatureKeys.Reports, cancellationToken)
            .ConfigureAwait(false);
        if (gate.IsFailure)
        {
            return Result.Failure<ReportExportFileDto>(gate.ErrorCode!, gate.ErrorMessage!);
        }

        if (tenantContext.TenantId is null || tenantContext.UserId is null)
        {
            return Result.Failure<ReportExportFileDto>(ErrorCodes.TenantNotFound, ErrorCodes.TenantNotFound);
        }

        var fmt = (format ?? string.Empty).Trim().ToLowerInvariant();
        if (fmt is not ("xlsx" or "pdf"))
        {
            return Result.Failure<ReportExportFileDto>(
                ErrorCodes.ReportsValidationFailed,
                ErrorCodes.ReportsValidationFailed);
        }

        var prepared = await PrepareQueryAsync(query with { Page = 1, PageSize = ReportRange.MaxExportRows }, cancellationToken)
            .ConfigureAwait(false);
        if (prepared.IsFailure)
        {
            return Result.Failure<ReportExportFileDto>(prepared.ErrorCode!, prepared.ErrorMessage!);
        }

        var (asOf, scoped, _, _, sortBy, sortDesc) = prepared.Value!;
        var count = await scoped.CountAsync(cancellationToken).ConfigureAwait(false);
        if (count > ReportRange.MaxExportRows)
        {
            return Result.Failure<ReportExportFileDto>(
                ErrorCodes.ReportsExportTooLarge,
                ErrorCodes.ReportsExportTooLarge);
        }

        var rows = await ProjectRows(ApplySort(scoped, sortBy, sortDesc))
            .Take(ReportRange.MaxExportRows)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var arabic = IsArabic(acceptLanguage);
        IReadOnlyList<string> headers = arabic
            ? new[]
            {
                "الرقم الوظيفي", "الاسم (عربي)", "الاسم (إنجليزي)", "الفرع", "القسم", "الحالة", "نوع العقد", "تاريخ التعيين",
            }
            : new[]
            {
                "Employee number", "Name (AR)", "Name (EN)", "Branch", "Department", "Status", "Contract type", "Hire date",
            };

        var tableRows = rows.Select(r => (IReadOnlyList<string>)
        [
            r.EmployeeNumber,
            r.FullNameAr,
            r.FullNameEn ?? string.Empty,
            arabic ? r.BranchNameAr : (r.BranchNameEn ?? r.BranchNameAr),
            arabic
                ? (r.DepartmentNameAr ?? Unassigned(arabic))
                : (r.DepartmentNameEn ?? r.DepartmentNameAr ?? Unassigned(arabic)),
            r.Status,
            r.ContractType,
            r.HireDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        ]).ToList();

        var title = arabic ? "تقرير عدد الموظفين" : "Headcount report";
        var model = new ReportTableModel(
            title,
            "headcount",
            headers,
            tableRows,
            new Dictionary<string, string>
            {
                [arabic ? "اعتباراً من" : "As of"] = asOf.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            });

        var bytes = fmt == "xlsx"
            ? exportService.ExportExcel(model)
            : exportService.ExportPdf(model, arabic);

        var tz = await db.Tenants.AsNoTracking()
            .Where(t => t.Id == tenantContext.TenantId.Value)
            .Select(t => t.TimeZone)
            .SingleAsync(cancellationToken)
            .ConfigureAwait(false);
        var localNow = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, TenantTimeZones.Resolve(tz));
        var stamp = localNow.ToString("yyyyMMdd_HHmm", CultureInfo.InvariantCulture);
        var fileName = $"headcount_{stamp}.{fmt}";
        var contentType = fmt == "xlsx"
            ? "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
            : "application/pdf";

        var filterJson = JsonSerializer.Serialize(new
        {
            asOf,
            query.BranchId,
            query.DepartmentId,
            query.Statuses,
            query.ContractTypes,
            format = fmt,
        });

        await audit.WriteAsync(
                tenantContext.TenantId.Value,
                tenantContext.UserId,
                AuditActions.ReportExported,
                "Report",
                null,
                null,
                JsonSerializer.Serialize(new
                {
                    reportKey = "headcount",
                    filters = filterJson,
                    rowCount = rows.Count,
                    format = fmt,
                }),
                cancellationToken)
            .ConfigureAwait(false);

        return Result.Success(new ReportExportFileDto(bytes, contentType, fileName, rows.Count));
    }

    private async Task<Result<(DateOnly AsOf, IQueryable<Employee> Scoped, int Page, int PageSize, string SortBy, bool SortDesc)>> PrepareQueryAsync(
        HeadcountReportQuery query,
        CancellationToken cancellationToken)
    {
        var timeZone = await db.Tenants.AsNoTracking()
            .Where(t => t.Id == tenantContext.TenantId!.Value)
            .Select(t => t.TimeZone)
            .SingleAsync(cancellationToken)
            .ConfigureAwait(false);

        var asOf = query.AsOf ?? TenantTimeZones.ToWorkDate(DateTimeOffset.UtcNow, timeZone);
        var cutoff = HeadcountAsOf.UpdatedAtCutoffUtc(asOf, timeZone);

        var scope = await EmployeeDataScope.ResolveAsync(db, tenantContext.UserId!.Value, cancellationToken)
            .ConfigureAwait(false);
        var scoped = EmployeeDataScope.Apply(
            db.Employees.AsNoTracking(),
            scope.Scope,
            tenantContext.UserId.Value,
            scope.LinkedEmployeeId,
            scope.DepartmentId,
            scope.BranchId);

        scoped = HeadcountAsOf.Apply(scoped, asOf, cutoff);

        if (query.BranchId is Guid branchId)
        {
            scoped = scoped.Where(e => e.BranchId == branchId);
        }

        if (query.DepartmentId is Guid departmentId)
        {
            scoped = scoped.Where(e => e.DepartmentId == departmentId);
        }

        var statuses = ParseStatuses(query.Statuses);
        if (statuses.Count > 0)
        {
            scoped = scoped.Where(e => statuses.Contains(e.Status));
        }

        var contracts = ParseContracts(query.ContractTypes);
        if (contracts.Count > 0)
        {
            scoped = scoped.Where(e => contracts.Contains(e.ContractType));
        }

        var page = ReportRange.ClampPage(query.Page);
        var pageSize = ReportRange.ClampPageSize(query.PageSize);
        var sortBy = string.IsNullOrWhiteSpace(query.SortBy) || !SortWhitelist.Contains(query.SortBy)
            ? "fullNameAr"
            : query.SortBy!;

        return Result.Success((asOf, scoped, page, pageSize, sortBy, query.SortDesc));
    }

    private IQueryable<HeadcountRowDto> ProjectRows(IQueryable<Employee> query) =>
        from e in query
        join b in db.Branches.AsNoTracking() on e.BranchId equals b.Id
        join d in db.Departments.AsNoTracking() on e.DepartmentId equals d.Id into dj
        from d in dj.DefaultIfEmpty()
        select new HeadcountRowDto(
            e.Id,
            e.EmployeeNumber,
            e.FullNameAr,
            e.FullNameEn,
            b.NameAr,
            b.NameEn,
            d != null ? d.NameAr : null,
            d != null ? d.NameEn : null,
            e.Status.ToString(),
            e.ContractType.ToString(),
            e.HireDate);

    private static IQueryable<Employee> ApplySort(IQueryable<Employee> query, string sortBy, bool desc) =>
        (sortBy.ToLowerInvariant(), desc) switch
        {
            ("employeenumber", false) => query.OrderBy(e => e.EmployeeNumber),
            ("employeenumber", true) => query.OrderByDescending(e => e.EmployeeNumber),
            ("fullnameen", false) => query.OrderBy(e => e.FullNameEn ?? e.FullNameAr),
            ("fullnameen", true) => query.OrderByDescending(e => e.FullNameEn ?? e.FullNameAr),
            ("hiredate", false) => query.OrderBy(e => e.HireDate),
            ("hiredate", true) => query.OrderByDescending(e => e.HireDate),
            ("status", false) => query.OrderBy(e => e.Status),
            ("status", true) => query.OrderByDescending(e => e.Status),
            ("contracttype", false) => query.OrderBy(e => e.ContractType),
            ("contracttype", true) => query.OrderByDescending(e => e.ContractType),
            ("branch", false) => query.OrderBy(e => e.BranchId),
            ("branch", true) => query.OrderByDescending(e => e.BranchId),
            ("department", false) => query.OrderBy(e => e.DepartmentId),
            ("department", true) => query.OrderByDescending(e => e.DepartmentId),
            (_, true) => query.OrderByDescending(e => e.FullNameAr),
            _ => query.OrderBy(e => e.FullNameAr),
        };

    private static List<EmployeeStatus> ParseStatuses(IReadOnlyList<string>? values)
    {
        var list = new List<EmployeeStatus>();
        if (values is null)
        {
            return list;
        }

        foreach (var raw in values)
        {
            foreach (var part in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (Enum.TryParse<EmployeeStatus>(part, true, out var status))
                {
                    list.Add(status);
                }
            }
        }

        return list;
    }

    private static List<ContractType> ParseContracts(IReadOnlyList<string>? values)
    {
        var list = new List<ContractType>();
        if (values is null)
        {
            return list;
        }

        foreach (var raw in values)
        {
            foreach (var part in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (Enum.TryParse<ContractType>(part, true, out var ct))
                {
                    list.Add(ct);
                }
            }
        }

        return list;
    }

    private static string Unassigned(bool arabic) => arabic ? "غير معيّن" : "Unassigned";

    private static bool IsArabic(string? acceptLanguage)
    {
        if (string.IsNullOrWhiteSpace(acceptLanguage))
        {
            return true;
        }

        var primary = acceptLanguage.Split(',').FirstOrDefault()?.Trim() ?? "ar";
        return primary.StartsWith("ar", StringComparison.OrdinalIgnoreCase);
    }
}
