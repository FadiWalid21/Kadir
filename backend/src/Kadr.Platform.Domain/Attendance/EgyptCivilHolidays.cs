namespace Kadr.Platform.Domain.Attendance;

/// <summary>
/// Fixed Egypt civil holidays seeded as [VERIFY] placeholders. No movable religious holidays.
/// </summary>
public static class EgyptCivilHolidays
{
    public static IReadOnlyList<(DateOnly StartsOn, DateOnly EndsOn, string NameAr, string NameEn)> Placeholders { get; } =
    [
        (new(2026, 1, 1), new(2026, 1, 1), "رأس السنة الميلادية [VERIFY]", "New Year's Day [VERIFY]"),
        (new(2026, 1, 7), new(2026, 1, 7), "عيد الميلاد المجيد [VERIFY]", "Coptic Christmas [VERIFY]"),
        (new(2026, 1, 25), new(2026, 1, 25), "عيد ثورة 25 يناير [VERIFY]", "Revolution Day [VERIFY]"),
        (new(2026, 4, 25), new(2026, 4, 25), "عيد تحرير سيناء [VERIFY]", "Sinai Liberation Day [VERIFY]"),
        (new(2026, 5, 1), new(2026, 5, 1), "عيد العمال [VERIFY]", "Labour Day [VERIFY]"),
        (new(2026, 7, 23), new(2026, 7, 23), "عيد ثورة 23 يوليو [VERIFY]", "Revolution Day (July) [VERIFY]"),
        (new(2026, 10, 6), new(2026, 10, 6), "عيد القوات المسلحة [VERIFY]", "Armed Forces Day [VERIFY]"),
    ];
}
