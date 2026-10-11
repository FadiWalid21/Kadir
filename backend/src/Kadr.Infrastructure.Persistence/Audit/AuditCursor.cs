using System.Globalization;
using System.Text;

namespace Kadr.Infrastructure.Persistence.Audit;

/// <summary>Opaque keyset cursor (timestamp + id) shared by the tenant and platform audit feeds.</summary>
internal static class AuditCursor
{
    public static string Encode(DateTimeOffset at, Guid id)
    {
        var raw = $"{at.UtcDateTime:O}|{id:N}";
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(raw));
    }

    public static bool TryDecode(string cursor, out DateTimeOffset at, out Guid id)
    {
        at = default;
        id = default;
        try
        {
            var raw = Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
            var parts = raw.Split('|', 2);
            if (parts.Length != 2)
            {
                return false;
            }

            if (!DateTimeOffset.TryParse(parts[0], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out at))
            {
                return false;
            }

            return Guid.TryParseExact(parts[1], "N", out id);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
