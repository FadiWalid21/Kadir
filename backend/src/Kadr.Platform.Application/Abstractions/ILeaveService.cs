using Kadr.Platform.Application.Leave;
using Kadr.SharedKernel.Results;

namespace Kadr.Platform.Application.Abstractions;

public interface ILeaveService
{
    Task<Result<IReadOnlyList<LeaveTypeDto>>> ListLeaveTypesAsync(CancellationToken cancellationToken = default);

    Task<Result<LeaveTypeDto>> UpdateLeaveTypeAsync(
        Guid id,
        UpdateLeaveTypeRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<LeaveBalanceDto>>> GetMyBalancesAsync(
        int? year,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<LeaveBalanceDto>>> ListBalancesAsync(
        int? year,
        Guid? employeeId,
        Guid? leaveTypeId,
        CancellationToken cancellationToken = default);

    Task<Result<LeaveBalanceDto>> AdjustBalanceAsync(
        AdjustLeaveBalanceRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<LeaveRequestDto>> CreateLeaveRequestAsync(
        CreateLeaveRequestBody request,
        CancellationToken cancellationToken = default);

    Task<Result<PagedLeaveRequestsDto>> ListMyLeaveRequestsAsync(
        string? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<Result<LeaveRequestDto>> GetLeaveRequestAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Result<LeaveRequestDto>> CancelLeaveRequestAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Result<PermissionRequestDto>> CreatePermissionAsync(
        CreatePermissionRequestBody request,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<PermissionRequestDto>>> ListMyPermissionsAsync(
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<WorkflowDefinitionDto>>> ListWorkflowsAsync(
        CancellationToken cancellationToken = default);

    Task<Result<WorkflowDefinitionDto>> UpsertWorkflowAsync(
        Guid? id,
        UpsertWorkflowRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<PagedRequestInboxDto>> ListInboxAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<Result> ApproveTaskAsync(Guid taskId, CancellationToken cancellationToken = default);

    Task<Result> RejectTaskAsync(
        Guid taskId,
        RejectRequestBody request,
        CancellationToken cancellationToken = default);
}
