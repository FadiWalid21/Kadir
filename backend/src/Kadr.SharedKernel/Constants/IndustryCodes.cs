namespace Kadr.SharedKernel.Constants;

public static class IndustryCodes
{
    public const string Retail = "retail";
    public const string Clinic = "clinic";
    public const string Factory = "factory";
    public const string Construction = "construction";
    public const string Office = "office";

    public static readonly IReadOnlyList<IndustryCatalogItem> Catalog =
    [
        new(Retail, "التجزئة والمتاجر", "Retail & Stores"),
        new(Clinic, "العيادات والرعاية الصحية", "Clinics & Healthcare"),
        new(Factory, "المصانع والإنتاج", "Factories & Production"),
        new(Construction, "المقاولات والتشييد", "Construction"),
        new(Office, "المكاتب والخدمات", "Office & Services"),
    ];

    public static readonly HashSet<string> Allowed = new(
        Catalog.Select(i => i.Code),
        StringComparer.OrdinalIgnoreCase);

    public static bool IsAllowed(string? code) =>
        !string.IsNullOrWhiteSpace(code) && Allowed.Contains(code.Trim());
}

public sealed record IndustryCatalogItem(string Code, string NameAr, string NameEn);
