namespace Kadr.Platform.Application.Templates;

public sealed record TenantTemplatesOverviewDto(
    string IndustryCode,
    string IndustryNameAr,
    string IndustryNameEn,
    int AppliedVersion,
    int LatestVersion,
    bool UpdateAvailable,
    IReadOnlyList<string> MissingKeys);

public sealed record PagedResultDto<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);

public sealed record TemplateItemDto(
    Guid Id,
    string Section,
    string? SourceKey,
    int SourceVersion,
    bool IsCustomized,
    bool IsActive,
    string NameAr,
    string NameEn,
    IReadOnlyDictionary<string, object?> Fields);

public sealed record UpdateShiftTemplateRequest(
    string NameAr,
    string NameEn,
    string StartTime,
    string EndTime,
    int GraceMinutes);

public sealed record UpdateLeaveTypeTemplateRequest(
    string NameAr,
    string NameEn,
    bool Paid,
    int? DaysPerYear,
    bool RequiresApproval,
    bool RequiresAttachment);

public sealed record UpdateDocumentTypeTemplateRequest(
    string NameAr,
    string NameEn,
    bool HasExpiry,
    IReadOnlyList<int> AlertDays);

public sealed record UpdateNamedTemplateRequest(string NameAr, string NameEn);

public sealed record SetActiveRequest(bool IsActive);

public sealed record AddMissingResultDto(int AddedCount, IReadOnlyList<string> AddedKeys);
