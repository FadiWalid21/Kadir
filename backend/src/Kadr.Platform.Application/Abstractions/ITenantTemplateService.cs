using Kadr.Platform.Application.Templates;
using Kadr.SharedKernel.Results;

namespace Kadr.Platform.Application.Abstractions;

public interface ITenantTemplateService
{
    Task<Result<TenantTemplatesOverviewDto>> GetOverviewAsync(CancellationToken cancellationToken = default);

    Task<Result<PagedResultDto<TemplateItemDto>>> ListSectionAsync(
        string section,
        int page,
        int pageSize,
        string? q,
        bool? active,
        CancellationToken cancellationToken = default);

    Task<Result<TemplateItemDto>> CreateShiftAsync(
        UpdateShiftTemplateRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<TemplateItemDto>> CreateLeaveTypeAsync(
        UpdateLeaveTypeTemplateRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<TemplateItemDto>> CreateDocumentTypeAsync(
        UpdateDocumentTypeTemplateRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<TemplateItemDto>> CreateJobTitleAsync(
        UpdateNamedTemplateRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<TemplateItemDto>> CreateDepartmentAsync(
        UpdateNamedTemplateRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<TemplateItemDto>> UpdateShiftAsync(
        Guid id,
        UpdateShiftTemplateRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<TemplateItemDto>> UpdateLeaveTypeAsync(
        Guid id,
        UpdateLeaveTypeTemplateRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<TemplateItemDto>> UpdateDocumentTypeAsync(
        Guid id,
        UpdateDocumentTypeTemplateRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<TemplateItemDto>> UpdateJobTitleAsync(
        Guid id,
        UpdateNamedTemplateRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<TemplateItemDto>> UpdateDepartmentAsync(
        Guid id,
        UpdateNamedTemplateRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<TemplateItemDto>> ResetAsync(
        string section,
        Guid id,
        CancellationToken cancellationToken = default);

    Task<Result<TemplateItemDto>> SetActiveAsync(
        string section,
        Guid id,
        bool isActive,
        CancellationToken cancellationToken = default);

    Task<Result> DeleteAsync(
        string section,
        Guid id,
        CancellationToken cancellationToken = default);

    Task<Result<AddMissingResultDto>> AddMissingAsync(CancellationToken cancellationToken = default);
}
