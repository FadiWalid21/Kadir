namespace Kadr.Api.Options;

public sealed class DatabaseHostOptions
{
    public const string SectionName = "Database";

    /// <summary>When true, run EF migrations and seeders at startup (demo hosts).</summary>
    public bool MigrateOnStartup { get; set; }
}
