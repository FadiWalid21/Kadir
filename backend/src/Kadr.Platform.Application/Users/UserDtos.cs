using Kadr.SharedKernel.Tenancy;

namespace Kadr.Platform.Application.Users;

public sealed record UserListItemDto(
    Guid Id,
    string FullName,
    string Email,
    Guid RoleId,
    string RoleName,
    bool IsSystemRole,
    bool IsActive,
    DataScope DataScope,
    DateTimeOffset? LastLoginAt);

public sealed record UserDetailDto(
    Guid Id,
    string FullName,
    string Email,
    Guid RoleId,
    string RoleName,
    bool IsSystemRole,
    bool IsActive,
    DataScope DataScope,
    DateTimeOffset? LastLoginAt,
    DateTimeOffset CreatedAt);

public sealed record UpdateUserRequest(
    string FullName,
    Guid RoleId,
    bool IsActive,
    DataScope DataScope);

public sealed record PagedUsersDto(
    IReadOnlyList<UserListItemDto> Items,
    int Page,
    int PageSize,
    int TotalCount);
