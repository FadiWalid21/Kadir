namespace Kadr.Platform.Application.Home;

public sealed record HomeDashboardDto(
    HomeGreetingDto Greeting,
    HomeChecklistDto? Checklist,
    HomeEmployeeSectionDto? Employee,
    HomeManagerSectionDto? Manager,
    HomeHrSectionDto? Hr);

public sealed record HomeGreetingDto(
    string FullName,
    string TenantName,
    string? LogoUrl);

public sealed record HomeChecklistDto(
    int DoneCount,
    int TotalCount,
    bool IsComplete,
    IReadOnlyList<HomeChecklistItemDto> Items);

public sealed record HomeChecklistItemDto(
    string Key,
    bool Done,
    string LinkPath);

public sealed record HomeEmployeeSectionDto(
    HomePunchSummaryDto? Punch,
    IReadOnlyList<HomeBalanceSummaryDto> LeaveBalances,
    IReadOnlyList<HomePendingRequestDto> MyPendingRequests);

public sealed record HomePunchSummaryDto(
    bool CanPunch,
    string? NextAction,
    DateTimeOffset? LastPunchAt,
    string? Status);

public sealed record HomeBalanceSummaryDto(
    Guid LeaveTypeId,
    string NameAr,
    string NameEn,
    decimal Remaining,
    decimal Entitled);

public sealed record HomePendingRequestDto(
    Guid Id,
    string Kind,
    string Status,
    DateOnly? StartDate,
    DateOnly? EndDate,
    string? TypeNameAr,
    string? TypeNameEn);

public sealed record HomeManagerSectionDto(
    int PendingApprovalCount,
    IReadOnlyList<HomeApprovalItemDto> PendingApprovals,
    HomeTeamAttendanceDto TeamToday);

public sealed record HomeApprovalItemDto(
    Guid TaskId,
    string RequestType,
    string EmployeeNameAr,
    string? EmployeeNameEn,
    DateTimeOffset CreatedAt);

public sealed record HomeTeamAttendanceDto(int Present, int Late, int Absent, int OnLeave);

public sealed record HomeHrSectionDto(
    int Headcount,
    int LateToday,
    int AbsentToday,
    int PendingWorkflowCount,
    IReadOnlyList<HomeExpiringDocumentDto> ExpiringDocuments);

public sealed record HomeExpiringDocumentDto(
    Guid DocumentId,
    Guid EmployeeId,
    string EmployeeNameAr,
    string? EmployeeNameEn,
    string DocumentTypeAr,
    string? DocumentTypeEn,
    DateOnly? ExpiryDate);
