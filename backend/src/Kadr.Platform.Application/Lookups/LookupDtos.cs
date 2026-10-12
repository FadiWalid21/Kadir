namespace Kadr.Platform.Application.Lookups;

public sealed record LookupItemDto(Guid Id, string Name);

public sealed record PagedLookupDto(
    IReadOnlyList<LookupItemDto> Items,
    int Page,
    int PageSize,
    int TotalCount);
