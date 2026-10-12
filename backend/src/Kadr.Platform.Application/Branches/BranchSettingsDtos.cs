namespace Kadr.Platform.Application.Branches;

public sealed record BranchListItemDto(
    Guid Id,
    Guid LegalEntityId,
    string LegalEntityName,
    string NameAr,
    string NameEn,
    string? Address,
    bool IsDefault,
    bool IsActive);

public sealed record PagedBranchesDto(
    IReadOnlyList<BranchListItemDto> Items,
    int Page,
    int PageSize,
    int TotalCount);

public sealed record CreateBranchRequest(
    Guid LegalEntityId,
    string NameAr,
    string NameEn,
    string? Address);

public sealed record UpdateBranchRequest(
    Guid LegalEntityId,
    string NameAr,
    string NameEn,
    string? Address);

public sealed record SetBranchActiveRequest(bool IsActive);
