using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Kadr.Platform.Application.Tenants;

public static partial class TenantSlug
{
    private static readonly HashSet<string> Reserved =
    [
        "www", "api", "platform", "admin", "app", "mail", "ftp", "cdn", "static",
    ];

    public static string FromName(string companyName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(companyName);

        var normalized = companyName.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);

        foreach (var ch in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (category == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (ch is >= 'a' and <= 'z' or >= '0' and <= '9')
            {
                builder.Append(ch);
            }
            else if (ch is ' ' or '-' or '_' or '.')
            {
                builder.Append('-');
            }
        }

        var slug = HyphenCollapse().Replace(builder.ToString(), "-").Trim('-');
        if (slug.Length == 0)
        {
            slug = "company";
        }

        if (slug.Length > 63)
        {
            slug = slug[..63].TrimEnd('-');
        }

        if (Reserved.Contains(slug))
        {
            slug = $"{slug}-co";
        }

        return slug;
    }

    public static string WithSuffix(string baseSlug, Guid entropy)
    {
        var suffix = ToBase36(entropy).PadLeft(4, '0')[^4..];
        var maxBase = 63 - 1 - suffix.Length;
        var truncated = baseSlug.Length > maxBase ? baseSlug[..maxBase].TrimEnd('-') : baseSlug;
        return $"{truncated}-{suffix}";
    }

    private static string ToBase36(Guid value)
    {
        var bytes = value.ToByteArray();
        var number = BitConverter.ToUInt32(bytes, 0);
        const string alphabet = "0123456789abcdefghijklmnopqrstuvwxyz";
        var result = new StringBuilder();
        do
        {
            result.Insert(0, alphabet[(int)(number % 36)]);
            number /= 36;
        }
        while (number > 0);

        return result.ToString();
    }

    [GeneratedRegex("-{2,}")]
    private static partial Regex HyphenCollapse();
}
