namespace Kadr.Platform.Application.Abstractions;

/// <summary>Test seam: throw after tenant insert to verify transactional rollback.</summary>
public interface IRegisterTenantFailureInjector
{
    void AfterTenantInserted();
}

public sealed class NoOpRegisterTenantFailureInjector : IRegisterTenantFailureInjector
{
    public void AfterTenantInserted()
    {
    }
}
