using Kadr.SharedKernel.Constants;
using Kadr.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace Kadr.Infrastructure.Persistence.Users;

public interface IEmailUniquenessChecker
{
    Task<Result> EnsureAvailableAsync(
        Guid tenantId,
        string normalizedEmail,
        CancellationToken cancellationToken = default);
}

public sealed class EmailUniquenessChecker(KadrDbContext db) : IEmailUniquenessChecker
{
    public async Task<Result> EnsureAvailableAsync(
        Guid tenantId,
        string normalizedEmail,
        CancellationToken cancellationToken = default)
    {
        var exists = await db.Users
            .AnyAsync(
                u => u.TenantId == tenantId && u.NormalizedEmail == normalizedEmail,
                cancellationToken)
            .ConfigureAwait(false);

        return exists
            ? Result.Failure(ErrorCodes.AuthEmailTaken, ErrorCodes.AuthEmailTaken)
            : Result.Success();
    }
}
