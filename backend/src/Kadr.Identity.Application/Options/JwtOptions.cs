using System.ComponentModel.DataAnnotations;

namespace Kadr.Identity.Application.Options;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required]
    [MinLength(32, ErrorMessage = "Jwt:SigningKey is required and must be at least 32 characters (use user-secrets or env).")]
    public string SigningKey { get; set; } = string.Empty;

    [Required]
    public string Issuer { get; set; } = "kadr";

    [Required]
    public string Audience { get; set; } = "kadr-api";

    [Range(1, 120)]
    public int AccessTokenMinutes { get; set; } = 15;

    [Range(1, 365)]
    public int RefreshTokenDays { get; set; } = 30;
}
