namespace Kadr.Identity.Application.Security;

/// <summary>Precomputed hash used when the email is unknown so verify timing stays comparable.</summary>
public sealed class DummyPasswordHash
{
    public string Value { get; }

    public DummyPasswordHash(IPasswordHasher passwordHasher)
    {
        Value = passwordHasher.HashPassword("KadrDummyPassword!NotARealUser");
    }
}
