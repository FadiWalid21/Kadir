using System.ComponentModel.DataAnnotations;

namespace Kadr.Identity.Application.Options;

public sealed class PlatformAdminOptions
{
    public const string SectionName = "PlatformAdmin";

    /// <summary>Corporate email for the seeded platform owner. Empty skips seeding.</summary>
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    /// <summary>Password used once at seed time. Never logged.</summary>
    public string Password { get; set; } = string.Empty;

    public string FullName { get; set; } = "Platform Owner";
}
