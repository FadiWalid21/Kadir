using Kadr.SharedKernel.Constants;

namespace Kadr.Platform.Application.Tenants;

public static class PasswordRules
{
    public const int MinimumLength = 10;

    public static string? Validate(string password, IReadOnlySet<string> commonPasswords)
    {
        if (string.IsNullOrEmpty(password) || password.Length < MinimumLength)
        {
            return ErrorCodes.AuthPasswordTooShort;
        }

        if (commonPasswords.Contains(password) || commonPasswords.Contains(password.ToLowerInvariant()))
        {
            return ErrorCodes.AuthPasswordTooCommon;
        }

        return null;
    }
}
