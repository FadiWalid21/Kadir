namespace Kadr.Identity.Application.Options;

public sealed class PasswordResetOptions
{
    public const string SectionName = "PasswordReset";

    /// <summary>Maximum forgot-password requests honoured per email address within the window.</summary>
    public int EmailPermitLimit { get; set; } = 3;

    public int EmailWindowMinutes { get; set; } = 15;
}
