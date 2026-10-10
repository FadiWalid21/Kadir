namespace Kadr.SharedKernel.Tenancy;

/// <summary>
/// Current tenant comes only from the authenticated JWT claim (never body/header/query).
/// </summary>
public interface ITenantContext
{
    Guid? TenantId { get; }
    Guid? UserId { get; }
    bool HasTenant { get; }
}
