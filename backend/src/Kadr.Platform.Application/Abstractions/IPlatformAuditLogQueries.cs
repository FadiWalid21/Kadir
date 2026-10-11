using Kadr.Platform.Application.PlatformAudit;
using Kadr.SharedKernel.Results;

namespace Kadr.Platform.Application.Abstractions;

/// <summary>Cross-tenant audit feed for platform admins. Callers must be platform admins.</summary>
public interface IPlatformAuditLogQueries
{
    Task<Result<PlatformAuditLogListResultDto>> ListAsync(
        PlatformAuditLogQuery query,
        CancellationToken cancellationToken = default);
}
