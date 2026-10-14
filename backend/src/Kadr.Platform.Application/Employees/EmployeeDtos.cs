using Kadr.Platform.Domain.Employees;

namespace Kadr.Platform.Application.Employees;

public sealed record EmployeeListItemDto(
    Guid Id,
    string EmployeeNumber,
    string FullNameAr,
    string? FullNameEn,
    string? Email,
    string? Phone,
    string? NationalId,
    string Status,
    string? JobTitleNameAr,
    string? JobTitleNameEn,
    string? DepartmentNameAr,
    string? DepartmentNameEn,
    string? BranchName,
    string? ManagerNameAr,
    Guid? ManagerId,
    DateOnly HireDate);

public sealed record EmployeeStatusCountsDto(
    int All,
    int Active,
    int Probation,
    int OnLeave,
    int Suspended,
    int Terminated);

public sealed record EmployeeSummaryDto(
    EmployeeStatusCountsDto StatusCounts,
    int ActiveForCapacity,
    int? EmployeeCap);

public sealed record PagedEmployeesDto(
    IReadOnlyList<EmployeeListItemDto> Items,
    int Page,
    int PageSize,
    int TotalCount,
    EmployeeSummaryDto Summary);

public sealed record EmergencyContactDto(Guid Id, string Name, string Relationship, string Phone, int SortOrder);

public sealed record EmployeeDetailDto(
    Guid Id,
    string EmployeeNumber,
    string FullNameAr,
    string? FullNameEn,
    string? NationalId,
    string? NationalIdMasked,
    string Gender,
    DateOnly? DateOfBirth,
    string? Email,
    string? Phone,
    string? Address,
    Guid? PhotoFileId,
    Guid LegalEntityId,
    Guid BranchId,
    string BranchName,
    Guid? DepartmentId,
    string? DepartmentNameAr,
    string? DepartmentNameEn,
    Guid? JobTitleId,
    string? JobTitleNameAr,
    string? JobTitleNameEn,
    Guid? ManagerEmployeeId,
    string? ManagerNameAr,
    DateOnly HireDate,
    DateOnly? ProbationEndDate,
    string ContractType,
    DateOnly? ContractStartDate,
    DateOnly? ContractEndDate,
    string Status,
    Guid? UserId,
    string? LinkedUserEmail,
    string? LinkedUserFullName,
    IReadOnlyList<EmergencyContactDto> EmergencyContacts,
    IReadOnlyList<EmployeeListItemDto> DirectReports,
    IReadOnlyList<EmployeeDocumentListItemDto> UrgentDocuments);

public sealed record EmergencyContactInput(string Name, string Relationship, string Phone);

public sealed record CreateEmployeeRequest(
    string FullNameAr,
    string? FullNameEn,
    string? NationalId,
    string? Gender,
    DateOnly? DateOfBirth,
    string? Email,
    string? Phone,
    string? Address,
    Guid? BranchId,
    Guid? DepartmentId,
    Guid? JobTitleId,
    Guid? ManagerEmployeeId,
    DateOnly HireDate,
    DateOnly? ProbationEndDate,
    string? ContractType,
    DateOnly? ContractStartDate,
    DateOnly? ContractEndDate,
    string? Status,
    string? EmployeeNumber,
    IReadOnlyList<EmergencyContactInput>? EmergencyContacts);

public sealed record UpdateEmployeeRequest(
    string FullNameAr,
    string? FullNameEn,
    string? NationalId,
    string? Gender,
    DateOnly? DateOfBirth,
    string? Email,
    string? Phone,
    string? Address,
    Guid BranchId,
    Guid? DepartmentId,
    Guid? JobTitleId,
    Guid? ManagerEmployeeId,
    DateOnly HireDate,
    DateOnly? ProbationEndDate,
    string? ContractType,
    DateOnly? ContractStartDate,
    DateOnly? ContractEndDate,
    IReadOnlyList<EmergencyContactInput>? EmergencyContacts);

public sealed record ChangeEmployeeStatusRequest(string Status);

public sealed record EmployeeDocumentListItemDto(
    Guid Id,
    Guid DocumentTypeId,
    string DocumentTypeNameAr,
    string DocumentTypeNameEn,
    string? ReferenceNumber,
    DateOnly? IssuedOn,
    DateOnly? ExpiresOn,
    string ExpiryStatus,
    Guid FileObjectId,
    string? FileName);

public sealed record UploadEmployeeDocumentRequest(
    Guid DocumentTypeId,
    string? ReferenceNumber,
    DateOnly? IssuedOn,
    DateOnly? ExpiresOn);

public sealed record ExpiringDocumentRowDto(
    Guid DocumentId,
    Guid EmployeeId,
    string EmployeeNameAr,
    string? JobTitleNameAr,
    string? DepartmentNameAr,
    Guid DocumentTypeId,
    string DocumentTypeNameAr,
    string DocumentTypeNameEn,
    string? ReferenceNumber,
    DateOnly? ExpiresOn,
    string ExpiryStatus,
    int? DaysRemaining);

public sealed record ExpiringDocumentsResultDto(
    IReadOnlyList<ExpiringDocumentRowDto> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int Within60,
    int Within30,
    int Within7,
    int Expired);

public sealed record OrgDepartmentNodeDto(
    Guid Id,
    string NameAr,
    string NameEn,
    Guid? ParentId,
    Guid? ManagerEmployeeId,
    string? ManagerNameAr,
    int EmployeeCount,
    IReadOnlyList<OrgDepartmentNodeDto> Children);

public sealed record UpdateDepartmentHierarchyRequest(Guid? ParentId, Guid? ManagerEmployeeId);

public sealed record OrgLookupItemDto(Guid Id, string NameAr, string NameEn);

public sealed record DocumentTypeLookupDto(Guid Id, string NameAr, string NameEn, bool HasExpiry);

public sealed record OrgLookupsDto(
    IReadOnlyList<OrgLookupItemDto> Branches,
    IReadOnlyList<OrgLookupItemDto> Departments,
    IReadOnlyList<OrgLookupItemDto> JobTitles,
    IReadOnlyList<DocumentTypeLookupDto> DocumentTypes);

public sealed record ImportPreviewErrorDto(int Row, string Field, string Code, string Message);

public sealed record ImportPreviewResultDto(
    int ValidCount,
    int ErrorCount,
    IReadOnlyList<ImportPreviewErrorDto> Errors,
    IReadOnlyList<CreateEmployeeRequest> ValidRows);

public sealed record ImportCommitResultDto(int ImportedCount, int SkippedCount, IReadOnlyList<ImportPreviewErrorDto> Errors);

public static class DocumentExpiryStatus
{
    public const string Valid = "valid";
    public const string Soon = "soon";
    public const string Expired = "expired";
    public const string None = "none";

    public static string Compute(DateOnly? expiresOn, DateOnly today)
    {
        if (expiresOn is null)
        {
            return None;
        }

        if (expiresOn.Value < today)
        {
            return Expired;
        }

        var days = expiresOn.Value.DayNumber - today.DayNumber;
        return days <= 30 ? Soon : Valid;
    }
}
