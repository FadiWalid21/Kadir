using Kadr.Identity.Domain.Users;
using Microsoft.AspNetCore.Identity;

namespace Kadr.Identity.Application.Security;

public sealed class AspNetPasswordHasher : IPasswordHasher
{
    private readonly PasswordHasher<AppUser> _hasher = new();

    public string HashPassword(string password) =>
        _hasher.HashPassword(user: null!, password);

    public bool VerifyPassword(string hashedPassword, string providedPassword)
    {
        var result = _hasher.VerifyHashedPassword(user: null!, hashedPassword, providedPassword);
        return result is PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded;
    }
}
