using Kadr.Identity.Domain.Users;
using Microsoft.AspNetCore.Identity;

namespace Kadr.Identity.Application.Security;

public sealed class AspNetPasswordHasher : IPasswordHasher
{
    private readonly PasswordHasher<AppUser> _hasher = new();

    public string HashPassword(string password) =>
        _hasher.HashPassword(user: null!, password);
}
