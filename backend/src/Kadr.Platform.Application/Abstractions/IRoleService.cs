using Kadr.Platform.Application.Roles;
using Kadr.SharedKernel.Results;

namespace Kadr.Platform.Application.Abstractions;

public interface IRoleService
{
    Task<Result<IReadOnlyList<RoleListItemDto>>> ListAsync(CancellationToken cancellationToken = default);

    Task<Result<RoleDetailDto>> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Result<RoleDetailDto>> CreateAsync(CreateRoleRequest request, CancellationToken cancellationToken = default);

    Task<Result<RoleDetailDto>> UpdateAsync(Guid id, UpdateRoleRequest request, CancellationToken cancellationToken = default);

    Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<PermissionGroupDto>>> ListPermissionsAsync(CancellationToken cancellationToken = default);
}
