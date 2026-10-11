namespace Kadr.Platform.Application.Roles;

public sealed record RoleListItemDto(
    Guid Id,
    string Name,
    bool IsSystem,
    string? DescriptionAr,
    string? DescriptionEn,
    int PermissionCount,
    int UserCount);

public sealed record RoleDetailDto(
    Guid Id,
    string Name,
    bool IsSystem,
    string? DescriptionAr,
    string? DescriptionEn,
    IReadOnlyList<string> Permissions,
    int UserCount);

public sealed record CreateRoleRequest(string Name, string? DescriptionAr, string? DescriptionEn, IReadOnlyList<string> Permissions);

public sealed record UpdateRoleRequest(string Name, string? DescriptionAr, string? DescriptionEn, IReadOnlyList<string> Permissions);

public sealed record PermissionGroupDto(string Resource, IReadOnlyList<PermissionItemDto> Permissions);

public sealed record PermissionItemDto(string Code, string LabelKey);
