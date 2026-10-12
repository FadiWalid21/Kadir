namespace Kadr.Platform.Application.Payroll;

public sealed record PayComponentDto(
    Guid Id,
    string Key,
    string NameAr,
    string NameEn,
    string Kind,
    string CalcType,
    bool IsTaxable,
    bool IsInsurable,
    bool IsActive,
    string? SourceKey);

public sealed record PagedPayComponentsDto(
    IReadOnlyList<PayComponentDto> Items,
    int Page,
    int PageSize,
    int TotalCount);

public sealed record CreatePayComponentRequest(
    string Key,
    string NameAr,
    string NameEn,
    string Kind,
    string CalcType,
    bool IsTaxable,
    bool IsInsurable);

public sealed record UpdatePayComponentRequest(
    string NameAr,
    string NameEn,
    string Kind,
    string CalcType,
    bool IsTaxable,
    bool IsInsurable);
