using Kadr.Platform.Application.Abstractions;
using Kadr.Platform.Application.Lookups;
using Kadr.Platform.Domain.Employees;
using Kadr.SharedKernel.Results;
using Kadr.SharedKernel.Text;
using Microsoft.EntityFrameworkCore;

namespace Kadr.Infrastructure.Persistence.Lookups;

public sealed class LookupService(KadrDbContext db) : ILookupService
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    public async Task<Result<PagedLookupDto>> SearchEmployeesAsync(
        string? q,
        int page,
        int pageSize,
        bool preferArabic,
        CancellationToken cancellationToken = default)
    {
        NormalizePaging(ref page, ref pageSize);
        var term = ArabicText.NormalizeForSearch(q);

        var query = db.Employees.AsNoTracking()
            .Where(e =>
                e.Status == EmployeeStatus.Active
                || e.Status == EmployeeStatus.Probation
                || e.Status == EmployeeStatus.OnLeave);

        if (term.Length > 0)
        {
            query = query.Where(e => e.SearchName.Contains(term));
        }

        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        var rows = await query
            .OrderBy(e => preferArabic ? e.FullNameAr : e.FullNameEn)
            .ThenBy(e => e.FullNameAr)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(e => new { e.Id, e.FullNameAr, e.FullNameEn })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var items = rows
            .Select(r => new LookupItemDto(r.Id, PickName(r.FullNameAr, r.FullNameEn, preferArabic)))
            .ToList();

        return Result.Success(new PagedLookupDto(items, page, pageSize, total));
    }

    public async Task<Result<PagedLookupDto>> SearchDepartmentsAsync(
        string? q,
        int page,
        int pageSize,
        bool preferArabic,
        CancellationToken cancellationToken = default)
    {
        NormalizePaging(ref page, ref pageSize);
        var term = ArabicText.NormalizeForSearch(q);

        var query = db.Departments.AsNoTracking().Where(d => d.IsActive);

        if (term.Length > 0)
        {
            query = query.Where(d => d.SearchName.Contains(term));
        }

        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        var rows = await query
            .OrderBy(d => preferArabic ? d.NameAr : d.NameEn)
            .ThenBy(d => d.NameAr)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(d => new { d.Id, d.NameAr, d.NameEn })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var items = rows
            .Select(r => new LookupItemDto(r.Id, PickName(r.NameAr, r.NameEn, preferArabic)))
            .ToList();

        return Result.Success(new PagedLookupDto(items, page, pageSize, total));
    }

    public async Task<Result<PagedLookupDto>> SearchBranchesAsync(
        string? q,
        int page,
        int pageSize,
        bool preferArabic,
        CancellationToken cancellationToken = default)
    {
        NormalizePaging(ref page, ref pageSize);
        var term = ArabicText.NormalizeForSearch(q);

        var rows = await db.Branches.AsNoTracking()
            .Where(b => b.IsActive)
            .Select(b => new { b.Id, b.NameAr, b.NameEn })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        IEnumerable<(Guid Id, string NameAr, string NameEn)> filtered = rows.Select(r => (r.Id, r.NameAr, r.NameEn));
        if (term.Length > 0)
        {
            filtered = filtered.Where(r =>
                ArabicText.NormalizeForSearch($"{r.NameAr} {r.NameEn}").Contains(term, StringComparison.Ordinal));
        }

        var ordered = filtered.OrderBy(r => r.NameAr, StringComparer.OrdinalIgnoreCase).ToList();
        var total = ordered.Count;
        var items = ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new LookupItemDto(r.Id, PickName(r.NameAr, r.NameEn, preferArabic)))
            .ToList();

        return Result.Success(new PagedLookupDto(items, page, pageSize, total));
    }

    public async Task<Result<PagedLookupDto>> SearchJobTitlesAsync(
        string? q,
        int page,
        int pageSize,
        bool preferArabic,
        CancellationToken cancellationToken = default)
    {
        NormalizePaging(ref page, ref pageSize);
        var term = ArabicText.NormalizeForSearch(q);

        var query = db.JobTitles.AsNoTracking().Where(j => j.IsActive);

        if (term.Length > 0)
        {
            query = query.Where(j => j.SearchName.Contains(term));
        }

        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        var rows = await query
            .OrderBy(j => preferArabic ? j.NameAr : j.NameEn)
            .ThenBy(j => j.NameAr)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(j => new { j.Id, j.NameAr, j.NameEn })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var items = rows
            .Select(r => new LookupItemDto(r.Id, PickName(r.NameAr, r.NameEn, preferArabic)))
            .ToList();

        return Result.Success(new PagedLookupDto(items, page, pageSize, total));
    }

    public async Task<Result<PagedLookupDto>> SearchLegalEntitiesAsync(
        string? q,
        int page,
        int pageSize,
        bool preferArabic,
        CancellationToken cancellationToken = default)
    {
        NormalizePaging(ref page, ref pageSize);
        var term = ArabicText.NormalizeForSearch(q);

        var rows = await db.LegalEntities.AsNoTracking()
            .Select(e => new { e.Id, e.Name })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        IEnumerable<(Guid Id, string Name)> filtered = rows.Select(r => (r.Id, r.Name));
        if (term.Length > 0)
        {
            filtered = filtered.Where(r =>
                ArabicText.NormalizeForSearch(r.Name).Contains(term, StringComparison.Ordinal));
        }

        var ordered = filtered.OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase).ToList();
        var total = ordered.Count;
        var items = ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new LookupItemDto(r.Id, r.Name))
            .ToList();

        _ = preferArabic;
        return Result.Success(new PagedLookupDto(items, page, pageSize, total));
    }

    private static string PickName(string nameAr, string? nameEn, bool preferArabic)
    {
        if (preferArabic)
        {
            return string.IsNullOrWhiteSpace(nameAr) ? (nameEn ?? string.Empty) : nameAr;
        }

        return string.IsNullOrWhiteSpace(nameEn) ? nameAr : nameEn;
    }

    private static void NormalizePaging(ref int page, ref int pageSize)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize < 1 ? DefaultPageSize : Math.Min(pageSize, MaxPageSize);
    }
}
