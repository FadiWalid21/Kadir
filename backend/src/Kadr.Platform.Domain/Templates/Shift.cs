using Kadr.SharedKernel.Domain;
using Kadr.SharedKernel.Ids;

namespace Kadr.Platform.Domain.Templates;

public sealed class Shift : AggregateRoot, ITenantOwned, IAuditable, ITemplateSourced
{
    private Shift()
    {
    }

    public Guid TenantId { get; private set; }
    public string NameAr { get; private set; } = string.Empty;
    public string NameEn { get; private set; } = string.Empty;
    public string SearchName { get; private set; } = string.Empty;
    public TimeOnly StartTime { get; private set; }
    public TimeOnly EndTime { get; private set; }
    public int GraceMinutes { get; private set; }
    public int BreakMinutes { get; private set; }
    public bool CrossesMidnight { get; private set; }
    public bool IsFlexible { get; private set; }
    public string? SourceKey { get; private set; }
    public Guid? SourceTemplateId { get; private set; }
    public int SourceVersion { get; private set; }
    public bool IsCustomized { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid? CreatedBy { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public Guid? UpdatedBy { get; private set; }

    public static Shift CreateFromTemplate(
        Guid tenantId,
        string sourceKey,
        Guid sourceTemplateId,
        int sourceVersion,
        string nameAr,
        string nameEn,
        TimeOnly startTime,
        TimeOnly endTime,
        int graceMinutes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(nameAr);
        ArgumentException.ThrowIfNullOrWhiteSpace(nameEn);
        ArgumentOutOfRangeException.ThrowIfNegative(graceMinutes);

        return new Shift
        {
            Id = GuidV7.NewGuid(),
            TenantId = tenantId,
            NameAr = nameAr.Trim(),
            NameEn = nameEn.Trim(),
            SearchName = BuildSearchName(nameAr, nameEn),
            StartTime = startTime,
            EndTime = endTime,
            GraceMinutes = graceMinutes,
            BreakMinutes = 0,
            CrossesMidnight = endTime <= startTime,
            IsFlexible = false,
            SourceKey = sourceKey.Trim(),
            SourceTemplateId = sourceTemplateId,
            SourceVersion = sourceVersion,
            IsCustomized = false,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };
    }

    public static Shift CreateCustom(
        Guid tenantId,
        string nameAr,
        string nameEn,
        TimeOnly startTime,
        TimeOnly endTime,
        int graceMinutes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nameAr);
        ArgumentException.ThrowIfNullOrWhiteSpace(nameEn);
        ArgumentOutOfRangeException.ThrowIfNegative(graceMinutes);

        return new Shift
        {
            Id = GuidV7.NewGuid(),
            TenantId = tenantId,
            NameAr = nameAr.Trim(),
            NameEn = nameEn.Trim(),
            SearchName = BuildSearchName(nameAr, nameEn),
            StartTime = startTime,
            EndTime = endTime,
            GraceMinutes = graceMinutes,
            BreakMinutes = 0,
            CrossesMidnight = endTime <= startTime,
            IsFlexible = false,
            SourceKey = null,
            SourceTemplateId = null,
            SourceVersion = 0,
            IsCustomized = true,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };
    }

    public void Update(
        string nameAr,
        string nameEn,
        TimeOnly startTime,
        TimeOnly endTime,
        int graceMinutes,
        int breakMinutes = 0,
        bool? crossesMidnight = null,
        bool isFlexible = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nameAr);
        ArgumentException.ThrowIfNullOrWhiteSpace(nameEn);
        ArgumentOutOfRangeException.ThrowIfNegative(graceMinutes);
        ArgumentOutOfRangeException.ThrowIfNegative(breakMinutes);
        NameAr = nameAr.Trim();
        NameEn = nameEn.Trim();
        SearchName = BuildSearchName(nameAr, nameEn);
        StartTime = startTime;
        EndTime = endTime;
        GraceMinutes = graceMinutes;
        BreakMinutes = breakMinutes;
        CrossesMidnight = crossesMidnight ?? endTime <= startTime;
        IsFlexible = isFlexible;
        IsCustomized = true;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void ResetFromTemplate(
        string nameAr,
        string nameEn,
        TimeOnly startTime,
        TimeOnly endTime,
        int graceMinutes,
        int sourceVersion)
    {
        NameAr = nameAr.Trim();
        NameEn = nameEn.Trim();
        SearchName = BuildSearchName(nameAr, nameEn);
        StartTime = startTime;
        EndTime = endTime;
        GraceMinutes = graceMinutes;
        BreakMinutes = 0;
        CrossesMidnight = endTime <= startTime;
        IsFlexible = false;
        SourceVersion = sourceVersion;
        IsCustomized = false;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void SetEngineFields(int breakMinutes, bool crossesMidnight, bool isFlexible)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(breakMinutes);
        BreakMinutes = breakMinutes;
        CrossesMidnight = crossesMidnight;
        IsFlexible = isFlexible;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void SetActive(bool isActive)
    {
        IsActive = isActive;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    private static string BuildSearchName(string nameAr, string nameEn) =>
        Kadr.SharedKernel.Text.ArabicText.NormalizeForSearch($"{nameAr} {nameEn}");
}
