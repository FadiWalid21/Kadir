namespace Kadr.Platform.Application.Reports;

public sealed record ReportHubItemDto(
    string Key,
    string TitleKey,
    string GroupKey,
    string Route,
    bool Available);

public sealed record HeadcountStatusCountDto(string Status, int Count);

public sealed record HeadcountSummaryDto(
    int Total,
    IReadOnlyList<HeadcountStatusCountDto> ByStatus);

public sealed record HeadcountRowDto(
    Guid EmployeeId,
    string EmployeeNumber,
    string FullNameAr,
    string? FullNameEn,
    string BranchNameAr,
    string? BranchNameEn,
    string? DepartmentNameAr,
    string? DepartmentNameEn,
    string Status,
    string ContractType,
    DateOnly HireDate);

public sealed record HeadcountReportDto(
    DateOnly AsOf,
    HeadcountSummaryDto Summary,
    IReadOnlyList<HeadcountRowDto> Items,
    int Page,
    int PageSize,
    int TotalCount);

public sealed record ReportExportFileDto(
    byte[] Content,
    string ContentType,
    string FileName,
    int RowCount);

public sealed record ReportTableModel(
    string Title,
    string ReportKey,
    IReadOnlyList<string> Headers,
    IReadOnlyList<IReadOnlyList<string>> Rows,
    IReadOnlyDictionary<string, string>? Meta = null);

public sealed record HeadcountReportQuery(
    DateOnly? AsOf,
    Guid? BranchId,
    Guid? DepartmentId,
    IReadOnlyList<string>? Statuses,
    IReadOnlyList<string>? ContractTypes,
    int Page,
    int PageSize,
    string? SortBy,
    bool SortDesc);
