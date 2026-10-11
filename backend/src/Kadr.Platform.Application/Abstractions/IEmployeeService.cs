using Kadr.Platform.Application.Employees;
using Kadr.SharedKernel.Results;

namespace Kadr.Platform.Application.Abstractions;

public interface IEmployeeService
{
    Task<Result<PagedEmployeesDto>> ListAsync(
        int page,
        int pageSize,
        string? q,
        string? status,
        Guid? departmentId,
        Guid? branchId,
        Guid? jobTitleId,
        Guid? managerId,
        CancellationToken cancellationToken = default);

    Task<Result<EmployeeDetailDto>> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Result<EmployeeDetailDto>> CreateAsync(
        CreateEmployeeRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<EmployeeDetailDto>> UpdateAsync(
        Guid id,
        UpdateEmployeeRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<EmployeeDetailDto>> ChangeStatusAsync(
        Guid id,
        ChangeEmployeeStatusRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<EmployeeDocumentListItemDto>>> ListDocumentsAsync(
        Guid employeeId,
        CancellationToken cancellationToken = default);

    Task<Result<EmployeeDocumentListItemDto>> UploadDocumentAsync(
        Guid employeeId,
        UploadEmployeeDocumentRequest meta,
        Stream content,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default);

    Task<Result> DeleteDocumentAsync(
        Guid employeeId,
        Guid documentId,
        CancellationToken cancellationToken = default);

    Task<Result<(Stream Stream, string FileName, string ContentType)>> OpenDocumentAsync(
        Guid employeeId,
        Guid documentId,
        CancellationToken cancellationToken = default);

    Task<Result<ExpiringDocumentsResultDto>> ListExpiringAsync(
        string window,
        string? q,
        Guid? documentTypeId,
        Guid? departmentId,
        Guid? branchId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
}

public interface IOrgService
{
    Task<Result<IReadOnlyList<OrgDepartmentNodeDto>>> GetTreeAsync(CancellationToken cancellationToken = default);

    Task<Result<OrgLookupsDto>> GetLookupsAsync(CancellationToken cancellationToken = default);

    Task<Result> UpdateDepartmentAsync(
        Guid departmentId,
        UpdateDepartmentHierarchyRequest request,
        CancellationToken cancellationToken = default);
}

public interface IEmployeeImportService
{
    Task<Result<byte[]>> GetTemplateAsync(CancellationToken cancellationToken = default);

    Task<Result<ImportPreviewResultDto>> PreviewAsync(
        Stream content,
        string fileName,
        CancellationToken cancellationToken = default);

    Task<Result<ImportCommitResultDto>> CommitAsync(
        IReadOnlyList<CreateEmployeeRequest> rows,
        CancellationToken cancellationToken = default);
}
