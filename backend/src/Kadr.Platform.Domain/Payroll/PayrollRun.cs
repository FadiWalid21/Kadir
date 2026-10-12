using Kadr.SharedKernel.Domain;
using Kadr.SharedKernel.Ids;

namespace Kadr.Platform.Domain.Payroll;

public sealed class PayrollRun : AggregateRoot, ITenantOwned
{
    private PayrollRun()
    {
    }

    public Guid TenantId { get; private set; }
    public Guid LegalEntityId { get; private set; }
    public DateOnly PeriodStart { get; private set; }
    public DateOnly PeriodEnd { get; private set; }
    public PayrollRunStatus Status { get; private set; }
    public string RulesSnapshotJson { get; private set; } = "{}";
    public Guid? ReopenedFromRunId { get; private set; }
    public Guid CreatedBy { get; private set; }
    public Guid? ApprovedBy { get; private set; }
    public DateTimeOffset? LockedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    /// <summary>PostgreSQL xmin concurrency token.</summary>
    public uint Version { get; private set; }
    public int WarningCount { get; private set; }
    public int Headcount { get; private set; }
    public decimal? TotalGross { get; private set; }
    public decimal? TotalNet { get; private set; }
    public decimal? TotalEmployerCost { get; private set; }

    public bool IsImmutable => Status is PayrollRunStatus.Locked or PayrollRunStatus.Superseded;

    public static PayrollRun CreateDraft(
        Guid tenantId,
        Guid legalEntityId,
        DateOnly periodStart,
        DateOnly periodEnd,
        Guid createdBy,
        Guid? reopenedFromRunId = null)
    {
        if (periodEnd < periodStart)
        {
            throw new ArgumentOutOfRangeException(nameof(periodEnd));
        }

        if (createdBy == Guid.Empty)
        {
            throw new ArgumentException("Created by is required.", nameof(createdBy));
        }

        return new PayrollRun
        {
            Id = GuidV7.NewGuid(),
            TenantId = tenantId,
            LegalEntityId = legalEntityId,
            PeriodStart = periodStart,
            PeriodEnd = periodEnd,
            Status = PayrollRunStatus.Draft,
            RulesSnapshotJson = "{}",
            ReopenedFromRunId = reopenedFromRunId,
            CreatedBy = createdBy,
            CreatedAt = DateTimeOffset.UtcNow,
        };
    }

    public void MarkCalculating()
    {
        EnsureMutable();
        if (Status != PayrollRunStatus.Draft)
        {
            throw new InvalidOperationException("Only draft runs can calculate.");
        }

        Status = PayrollRunStatus.Calculating;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void CompleteCalculation(
        string rulesSnapshotJson,
        int warningCount,
        int headcount,
        decimal totalGross,
        decimal totalNet,
        decimal totalEmployerCost)
    {
        if (Status != PayrollRunStatus.Calculating)
        {
            throw new InvalidOperationException("Run is not calculating.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(rulesSnapshotJson);
        RulesSnapshotJson = rulesSnapshotJson;
        WarningCount = warningCount;
        Headcount = headcount;
        TotalGross = decimal.Round(totalGross, 2, MidpointRounding.AwayFromZero);
        TotalNet = decimal.Round(totalNet, 2, MidpointRounding.AwayFromZero);
        TotalEmployerCost = decimal.Round(totalEmployerCost, 2, MidpointRounding.AwayFromZero);
        Status = PayrollRunStatus.Draft;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void FailCalculationBackToDraft()
    {
        if (Status == PayrollRunStatus.Calculating)
        {
            Status = PayrollRunStatus.Draft;
            UpdatedAt = DateTimeOffset.UtcNow;
        }
    }

    public void Submit()
    {
        EnsureMutable();
        if (Status != PayrollRunStatus.Draft)
        {
            throw new InvalidOperationException("Only draft runs can be submitted.");
        }

        Status = PayrollRunStatus.InReview;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Approve(Guid approvedBy, bool requireSecondApprover)
    {
        EnsureMutable();
        if (Status != PayrollRunStatus.InReview)
        {
            throw new InvalidOperationException("Only in-review runs can be approved.");
        }

        if (approvedBy == Guid.Empty)
        {
            throw new ArgumentException("Approver is required.", nameof(approvedBy));
        }

        if (requireSecondApprover && approvedBy == CreatedBy)
        {
            throw new InvalidOperationException("Second approver required.");
        }

        ApprovedBy = approvedBy;
        Status = PayrollRunStatus.Approved;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Lock()
    {
        EnsureMutable();
        if (Status != PayrollRunStatus.Approved)
        {
            throw new InvalidOperationException("Only approved runs can be locked.");
        }

        Status = PayrollRunStatus.Locked;
        LockedAt = DateTimeOffset.UtcNow;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void MarkSuperseded()
    {
        if (Status != PayrollRunStatus.Locked)
        {
            throw new InvalidOperationException("Only locked runs can be superseded.");
        }

        Status = PayrollRunStatus.Superseded;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    private void EnsureMutable()
    {
        if (IsImmutable)
        {
            throw new InvalidOperationException("Payroll run is immutable.");
        }
    }
}
