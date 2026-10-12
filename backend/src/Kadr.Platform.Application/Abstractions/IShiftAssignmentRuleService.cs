using Kadr.Platform.Application.Attendance;
using Kadr.SharedKernel.Results;

namespace Kadr.Platform.Application.Abstractions;

public interface IShiftAssignmentRuleService
{
    Task<Result<PagedShiftAssignmentRulesDto>> ListAsync(
        int page,
        int pageSize,
        string? targetType,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<ShiftAssignmentRuleDto>>> CreateAsync(
        CreateShiftAssignmentRulesRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<ShiftAssignmentRuleDto>> UpdateAsync(
        Guid id,
        UpdateShiftAssignmentRuleRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<ShiftAssignmentRuleDto>> EndAsync(
        Guid id,
        EndShiftAssignmentRuleRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<PreviewShiftAssignmentRulesDto>> PreviewAsync(
        PreviewShiftAssignmentRulesRequest request,
        CancellationToken cancellationToken = default);
}
