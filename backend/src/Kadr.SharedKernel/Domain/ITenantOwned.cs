namespace Kadr.SharedKernel.Domain;

public interface ITenantOwned
{
    Guid TenantId { get; }
}
