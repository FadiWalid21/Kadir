namespace Kadr.Api.Options;

public sealed class AuthHostOptions
{
    public const string SectionName = "Auth";

    /// <summary>
    /// When true (demo: Vercel SPA + MonsterASP API), refresh cookies use SameSite=None and Secure.
    /// Keep false for local same-site proxy.
    /// </summary>
    public bool CrossSiteCookies { get; set; }
}
