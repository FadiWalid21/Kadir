using System.Globalization;
using System.Text;

namespace Kadr.SharedKernel.Text;

/// <summary>
/// Normalizes Arabic (and mixed) text for search matching.
/// </summary>
public static class ArabicText
{
    public static string NormalizeForSearch(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var formD = value.Trim().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(formD.Length);
        foreach (var ch in formD)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (category is UnicodeCategory.NonSpacingMark or UnicodeCategory.Format)
            {
                continue;
            }

            // Tatweel
            if (ch is '\u0640')
            {
                continue;
            }

            sb.Append(FoldArabicLetter(ch));
        }

        return sb.ToString().ToLowerInvariant();
    }

    private static char FoldArabicLetter(char ch) =>
        ch switch
        {
            // Alef variants → ا
            '\u0622' or '\u0623' or '\u0625' or '\u0671' => '\u0627',
            // Yeh / alef maqsura → ي
            '\u0649' => '\u064A',
            // Teh marbuta → ه
            '\u0629' => '\u0647',
            _ => ch,
        };
}
