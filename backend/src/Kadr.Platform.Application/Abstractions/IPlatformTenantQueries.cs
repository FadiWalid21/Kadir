using Kadr.Platform.Application.PlatformTenants;
using Kadr.SharedKernel.Results;

namespace Kadr.Platform.Application.Abstractions;

/// <summary>Cross-tenant read side of the platform admin console. Callers must be platform admins.</summary>
public interface IPlatformTenantQueries
{
    Task<Result<PlatformTenantListResultDto>> ListAsync(
        PlatformTenantListQuery query,
        CancellationToken cancellationToken = default);

    Task<Result<PlatformTenantDetailDto>> GetAsync(Guid tenantId, CancellationToken cancellationToken = default);

    Task<Result<PlatformTenantAuditSummaryDto>> GetAuditSummaryAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);
}
