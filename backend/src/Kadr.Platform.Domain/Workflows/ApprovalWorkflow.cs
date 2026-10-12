using Kadr.SharedKernel.Domain;
using Kadr.SharedKernel.Ids;

namespace Kadr.Platform.Domain.Workflows;

public sealed class ApprovalWorkflow : AggregateRoot, ITenantOwned
{
    private ApprovalWorkflow()
    {
    }

    public Guid TenantId { get; private set; }
    public string NameAr { get; private set; } = string.Empty;
    public string NameEn { get; private set; } = string.Empty;
    public WorkflowRequestType RequestType { get; private set; }
    public bool IsActive { get; private set; } = true;
    public bool IsDefault { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }

    public static ApprovalWorkflow Create(
        Guid tenantId,
        string nameAr,
        string nameEn,
        WorkflowRequestType requestType,
        bool isDefault)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nameAr);
        return new ApprovalWorkflow
        {
            Id = GuidV7.NewGuid(),
            TenantId = tenantId,
            NameAr = nameAr.Trim(),
            NameEn = string.IsNullOrWhiteSpace(nameEn) ? nameAr.Trim() : nameEn.Trim(),
            RequestType = requestType,
            IsDefault = isDefault,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };
    }

    public void Update(string nameAr, string nameEn, bool isActive, bool isDefault)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nameAr);
        NameAr = nameAr.Trim();
        NameEn = string.IsNullOrWhiteSpace(nameEn) ? NameAr : nameEn.Trim();
        IsActive = isActive;
        IsDefault = isDefault;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Touch() => UpdatedAt = DateTimeOffset.UtcNow;

    public void ClearDefault() => IsDefault = false;
}
