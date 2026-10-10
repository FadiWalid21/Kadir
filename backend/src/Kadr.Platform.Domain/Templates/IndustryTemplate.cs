using Kadr.SharedKernel.Domain;
using Kadr.SharedKernel.Ids;

namespace Kadr.Platform.Domain.Templates;

public sealed class IndustryTemplate : AggregateRoot, IPlatformEntity, IAuditable
{
    private IndustryTemplate()
    {
    }

    public string Code { get; private set; } = string.Empty;
    public int Version { get; private set; }
    public string NameAr { get; private set; } = string.Empty;
    public string NameEn { get; private set; } = string.Empty;
    public string? DescriptionAr { get; private set; }
    public string? DescriptionEn { get; private set; }
    public string PayloadJson { get; private set; } = "{}";
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid? CreatedBy { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public Guid? UpdatedBy { get; private set; }

    public static IndustryTemplate Create(
        string code,
        int version,
        string nameAr,
        string nameEn,
        string payloadJson,
        string? descriptionAr = null,
        string? descriptionEn = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(nameAr);
        ArgumentException.ThrowIfNullOrWhiteSpace(nameEn);
        ArgumentException.ThrowIfNullOrWhiteSpace(payloadJson);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(version);

        return new IndustryTemplate
        {
            Id = GuidV7.NewGuid(),
            Code = code.Trim().ToLowerInvariant(),
            Version = version,
            NameAr = nameAr.Trim(),
            NameEn = nameEn.Trim(),
            DescriptionAr = string.IsNullOrWhiteSpace(descriptionAr) ? null : descriptionAr.Trim(),
            DescriptionEn = string.IsNullOrWhiteSpace(descriptionEn) ? null : descriptionEn.Trim(),
            PayloadJson = payloadJson,
            CreatedAt = DateTimeOffset.UtcNow,
        };
    }

    public void ReplacePayload(
        string nameAr,
        string nameEn,
        string payloadJson,
        string? descriptionAr,
        string? descriptionEn)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nameAr);
        ArgumentException.ThrowIfNullOrWhiteSpace(nameEn);
        ArgumentException.ThrowIfNullOrWhiteSpace(payloadJson);
        NameAr = nameAr.Trim();
        NameEn = nameEn.Trim();
        DescriptionAr = string.IsNullOrWhiteSpace(descriptionAr) ? null : descriptionAr.Trim();
        DescriptionEn = string.IsNullOrWhiteSpace(descriptionEn) ? null : descriptionEn.Trim();
        PayloadJson = payloadJson;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
