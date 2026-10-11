namespace Kadr.Api.Options;

public sealed class CorsHostOptions
{
    public const string SectionName = "Cors";

    /// <summary>Comma-separated or array of allowed browser origins (e.g. https://kadir-teal.vercel.app).</summary>
    public string[] Origins { get; set; } = [];
}
