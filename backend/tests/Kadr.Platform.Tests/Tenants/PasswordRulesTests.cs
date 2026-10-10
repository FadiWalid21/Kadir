using Kadr.Platform.Application.Tenants;
using Kadr.SharedKernel.Constants;
using Xunit;

namespace Kadr.Platform.Tests.Tenants;

public sealed class PasswordRulesTests
{
    private static readonly HashSet<string> Common = ["password123", "qwertyuiop", "welcome123"];

    [Theory]
    [InlineData("short", ErrorCodes.AuthPasswordTooShort)]
    [InlineData("123456789", ErrorCodes.AuthPasswordTooShort)]
    [InlineData("password123", ErrorCodes.AuthPasswordTooCommon)]
    [InlineData("PASSWORD123", ErrorCodes.AuthPasswordTooCommon)]
    [InlineData("Str0ngPass!!", null)]
    public void Validate_password_rules(string password, string? expectedError)
    {
        Assert.Equal(expectedError, PasswordRules.Validate(password, Common));
    }
}
