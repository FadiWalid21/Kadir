using System.Reflection;

namespace Kadr.Identity.Application.Security;

public sealed class EmbeddedCommonPasswordList : ICommonPasswordList
{
    public IReadOnlySet<string> Passwords { get; }

    public EmbeddedCommonPasswordList()
    {
        var assembly = typeof(EmbeddedCommonPasswordList).Assembly;
        var resourceName = assembly.GetManifestResourceNames()
            .Single(name => name.EndsWith("common-passwords.txt", StringComparison.Ordinal));

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException("common-passwords.txt resource missing.");
        using var reader = new StreamReader(stream);

        var set = new HashSet<string>(StringComparer.Ordinal);
        while (reader.ReadLine() is { } line)
        {
            var password = line.Trim();
            if (password.Length > 0 && !password.StartsWith('#'))
            {
                set.Add(password);
            }
        }

        Passwords = set;
    }
}
