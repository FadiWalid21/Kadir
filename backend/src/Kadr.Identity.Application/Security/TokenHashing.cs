using System.Security.Cryptography;
using System.Text;

namespace Kadr.Identity.Application.Security;

public static class TokenHashing
{
    public static string Hash(string rawToken)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexString(hash);
    }

    public static string CreateOpaqueToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
}
