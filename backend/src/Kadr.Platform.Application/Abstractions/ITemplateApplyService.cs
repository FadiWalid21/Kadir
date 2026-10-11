namespace Kadr.Platform.Application.Abstractions;

public interface ITemplateApplyService
{
    /// <summary>Copy all seed items for the industry into tenant tables (register / full apply).</summary>
    Task ApplyAsync(Guid tenantId, string industryCode, CancellationToken cancellationToken = default);

    /// <summary>Insert only keys present in the latest seed that the tenant does not already have.</summary>
    Task<int> AddMissingAsync(Guid tenantId, string industryCode, CancellationToken cancellationToken = default);

    /// <summary>
    /// Platform-admin industry change: deactivates template-sourced items that were never customized and are not
    /// part of the new industry template, then adds the new template's missing items. Nothing is hard-deleted.
    /// </summary>
    Task<TemplateReplaceResult> ReplaceUntouchedAsync(
        Guid tenantId,
        string industryCode,
        CancellationToken cancellationToken = default);
}

public sealed record TemplateReplaceResult(int AddedCount, int DeactivatedCount);
