using Kadr.Platform.Domain.Workflows;
using Kadr.SharedKernel.Results;

namespace Kadr.Platform.Application.Abstractions;

public interface IWorkflowEngine
{
    Task<Result<Guid>> StartAsync(
        WorkflowRequestType requestType,
        Guid subjectId,
        Guid requesterEmployeeId,
        CancellationToken cancellationToken = default);

    Task<Result> CancelForSubjectAsync(
        WorkflowRequestType requestType,
        Guid subjectId,
        CancellationToken cancellationToken = default);
}
