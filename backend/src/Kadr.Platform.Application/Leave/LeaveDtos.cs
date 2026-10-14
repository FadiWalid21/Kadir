namespace Kadr.Platform.Application.Leave;

public sealed record LeaveTypeDto(
    Guid Id,
    string NameAr,
    string NameEn,
    bool Paid,
    int? DaysPerYear,
    bool RequiresApproval,
    bool RequiresAttachment,
    bool IsActive,
    string? SourceKey,
    bool IsCustomized);

public sealed record UpdateLeaveTypeRequest(
    string NameAr,
    string NameEn,
    bool Paid,
    int? DaysPerYear,
    bool RequiresApproval,
    bool RequiresAttachment,
    bool IsActive);

public sealed record LeaveBalanceDto(
    Guid EmployeeId,
    string EmployeeNameAr,
    string EmployeeNameEn,
    Guid LeaveTypeId,
    string LeaveTypeNameAr,
    string LeaveTypeNameEn,
    int Year,
    decimal Entitled,
    decimal Accrued,
    decimal Used,
    decimal Adjustment,
    decimal CarriedIn,
    decimal Remaining);

public sealed record AdjustLeaveBalanceRequest(
    Guid EmployeeId,
    Guid LeaveTypeId,
    int Year,
    decimal DeltaDays,
    string Reason);

public sealed record CreateLeaveRequestBody(
    Guid LeaveTypeId,
    DateOnly StartDate,
    DateOnly EndDate,
    bool IsHalfDay,
    string? Reason,
    Guid? AttachmentFileId);

public sealed record LeaveAttachmentDto(Guid FileId);

public sealed record LeaveRequestDto(
    Guid Id,
    Guid EmployeeId,
    string EmployeeNameAr,
    string EmployeeNameEn,
    Guid LeaveTypeId,
    string LeaveTypeNameAr,
    string LeaveTypeNameEn,
    DateOnly StartDate,
    DateOnly EndDate,
    bool IsHalfDay,
    decimal Days,
    string? Reason,
    Guid? AttachmentFileId,
    string Status,
    Guid? WorkflowInstanceId,
    DateTimeOffset CreatedAt,
    string? RejectReason);

public sealed record CreatePermissionRequestBody(
    DateOnly WorkDate,
    TimeOnly StartTime,
    TimeOnly EndTime,
    string? Reason);

public sealed record PermissionRequestDto(
    Guid Id,
    Guid EmployeeId,
    string EmployeeNameAr,
    string EmployeeNameEn,
    DateOnly WorkDate,
    TimeOnly StartTime,
    TimeOnly EndTime,
    decimal Hours,
    string? Reason,
    string Status,
    Guid? WorkflowInstanceId,
    DateTimeOffset CreatedAt,
    string? RejectReason);

public sealed record PagedLeaveRequestsDto(
    IReadOnlyList<LeaveRequestDto> Items,
    int Page,
    int PageSize,
    int TotalCount);

public sealed record WorkflowDefinitionDto(
    Guid Id,
    string NameAr,
    string NameEn,
    string RequestType,
    bool IsActive,
    bool IsDefault,
    IReadOnlyList<WorkflowStepDto> Steps);

public sealed record WorkflowStepDto(
    Guid Id,
    int StepOrder,
    string ApproverKind,
    Guid? RoleId,
    Guid? UserId,
    bool IsFinal);

public sealed record UpsertWorkflowRequest(
    string NameAr,
    string NameEn,
    string RequestType,
    bool IsActive,
    bool IsDefault,
    IReadOnlyList<UpsertWorkflowStepRequest> Steps);

public sealed record UpsertWorkflowStepRequest(
    int StepOrder,
    string ApproverKind,
    Guid? RoleId,
    Guid? UserId,
    bool IsFinal);

public sealed record RequestInboxItemDto(
    Guid TaskId,
    Guid InstanceId,
    string RequestType,
    Guid SubjectId,
    Guid RequesterEmployeeId,
    string RequesterNameAr,
    string RequesterNameEn,
    int StepOrder,
    DateTimeOffset CreatedAt,
    string Summary);

public sealed record PagedRequestInboxDto(
    IReadOnlyList<RequestInboxItemDto> Items,
    int Page,
    int PageSize,
    int TotalCount);

public sealed record RejectRequestBody(string Reason);
