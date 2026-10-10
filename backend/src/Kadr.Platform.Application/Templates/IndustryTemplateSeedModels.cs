using System.Text.Json.Serialization;

namespace Kadr.Platform.Application.Templates;

public sealed class IndustryTemplateSeedDocument
{
    [JsonPropertyName("code")]
    public string Code { get; init; } = string.Empty;

    [JsonPropertyName("version")]
    public int Version { get; init; }

    [JsonPropertyName("nameAr")]
    public string NameAr { get; init; } = string.Empty;

    [JsonPropertyName("nameEn")]
    public string NameEn { get; init; } = string.Empty;

    [JsonPropertyName("descriptionAr")]
    public string? DescriptionAr { get; init; }

    [JsonPropertyName("descriptionEn")]
    public string? DescriptionEn { get; init; }

    [JsonPropertyName("items")]
    public IndustryTemplateSeedItems Items { get; init; } = new();
}

public sealed class IndustryTemplateSeedItems
{
    [JsonPropertyName("shifts")]
    public List<ShiftSeedItem> Shifts { get; init; } = [];

    [JsonPropertyName("leaveTypes")]
    public List<LeaveTypeSeedItem> LeaveTypes { get; init; } = [];

    [JsonPropertyName("documentTypes")]
    public List<DocumentTypeSeedItem> DocumentTypes { get; init; } = [];

    [JsonPropertyName("jobTitles")]
    public List<NamedSeedItem> JobTitles { get; init; } = [];

    [JsonPropertyName("departments")]
    public List<NamedSeedItem> Departments { get; init; } = [];
}

public sealed class NamedSeedItem
{
    [JsonPropertyName("key")]
    public string Key { get; init; } = string.Empty;

    [JsonPropertyName("nameAr")]
    public string NameAr { get; init; } = string.Empty;

    [JsonPropertyName("nameEn")]
    public string NameEn { get; init; } = string.Empty;
}

public sealed class ShiftSeedItem
{
    [JsonPropertyName("key")]
    public string Key { get; init; } = string.Empty;

    [JsonPropertyName("nameAr")]
    public string NameAr { get; init; } = string.Empty;

    [JsonPropertyName("nameEn")]
    public string NameEn { get; init; } = string.Empty;

    [JsonPropertyName("start")]
    public string Start { get; init; } = "09:00";

    [JsonPropertyName("end")]
    public string End { get; init; } = "17:00";

    [JsonPropertyName("graceMinutes")]
    public int GraceMinutes { get; init; }
}

public sealed class LeaveTypeSeedItem
{
    [JsonPropertyName("key")]
    public string Key { get; init; } = string.Empty;

    [JsonPropertyName("nameAr")]
    public string NameAr { get; init; } = string.Empty;

    [JsonPropertyName("nameEn")]
    public string NameEn { get; init; } = string.Empty;

    [JsonPropertyName("paid")]
    public bool Paid { get; init; }

    [JsonPropertyName("daysPerYear")]
    public int? DaysPerYear { get; init; }

    [JsonPropertyName("requiresApproval")]
    public bool RequiresApproval { get; init; } = true;

    [JsonPropertyName("requiresAttachment")]
    public bool RequiresAttachment { get; init; }

    [JsonPropertyName("verify")]
    public bool Verify { get; init; }
}

public sealed class DocumentTypeSeedItem
{
    [JsonPropertyName("key")]
    public string Key { get; init; } = string.Empty;

    [JsonPropertyName("nameAr")]
    public string NameAr { get; init; } = string.Empty;

    [JsonPropertyName("nameEn")]
    public string NameEn { get; init; } = string.Empty;

    [JsonPropertyName("hasExpiry")]
    public bool HasExpiry { get; init; }

    [JsonPropertyName("alertDays")]
    public List<int> AlertDays { get; init; } = [];
}
