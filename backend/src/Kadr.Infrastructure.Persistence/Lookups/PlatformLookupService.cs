using Kadr.Platform.Application.Abstractions;
using Kadr.Platform.Application.Lookups;
using Kadr.SharedKernel.Results;
using Kadr.SharedKernel.Text;
using Microsoft.EntityFrameworkCore;

namespace Kadr.Infrastructure.Persistence.Lookups;

public sealed class PlatformLookupService(KadrDbContext db) : IPlatformLookupService
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    public async Task<Result<PagedLookupDto>> SearchTenantsAsync(
        string? q,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize < 1 ? DefaultPageSize : Math.Min(pageSize, MaxPageSize);
        var term = ArabicText.NormalizeForSearch(q);

        var rows = await db.Tenants.IgnoreQueryFilters().AsNoTracking()
            .Select(t => new { t.Id, t.Name, t.Slug })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var filtered = rows.AsEnumerable();
        if (term.Length > 0)
        {
            filtered = filtered.Where(r =>
                ArabicText.NormalizeForSearch(r.Name).Contains(term, StringComparison.Ordinal)
                || ArabicText.NormalizeForSearch(r.Slug).Contains(term, StringComparison.Ordinal));
        }

        var ordered = filtered
            .OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var total = ordered.Count;
        var items = ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new LookupItemDto(r.Id, r.Name))
            .ToList();

        return Result.Success(new PagedLookupDto(items, page, pageSize, total));
    }
}
