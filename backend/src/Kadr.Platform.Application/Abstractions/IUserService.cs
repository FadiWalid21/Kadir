using Kadr.Platform.Application.Users;
using Kadr.SharedKernel.Results;

namespace Kadr.Platform.Application.Abstractions;

public interface IUserService
{
    Task<Result<PagedUsersDto>> ListAsync(
        int page,
        int pageSize,
        string? q,
        Guid? roleId,
        bool? active,
        CancellationToken cancellationToken = default);

    Task<Result<UserDetailDto>> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Result<UserDetailDto>> UpdateAsync(
        Guid id,
        UpdateUserRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<UserDetailDto>> SetEmployeeLinkAsync(
        Guid id,
        SetUserEmployeeRequest request,
        CancellationToken cancellationToken = default);
}
