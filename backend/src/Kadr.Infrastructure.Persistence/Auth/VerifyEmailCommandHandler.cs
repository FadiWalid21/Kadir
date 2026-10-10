using Kadr.Identity.Application.Auth;
using Kadr.Identity.Application.Security;
using Kadr.SharedKernel.Application;
using Kadr.SharedKernel.Constants;
using Kadr.SharedKernel.Results;
using Kadr.SharedKernel.Tenancy;
using Kadr.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace Kadr.Infrastructure.Persistence.Auth;

public sealed class VerifyEmailCommandHandler(
    KadrDbContext db,
    ITenantContextAccessor tenantContext,
    IClock clock) : ICommandHandler<VerifyEmailCommand>
{
    public async Task<Result> HandleAsync(VerifyEmailCommand command, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.Token))
        {
            return Result.Failure(ErrorCodes.AuthVerificationInvalid, ErrorCodes.AuthVerificationInvalid);
        }

        var hash = TokenHashing.Hash(command.Token.Trim());
        var token = await db.EmailVerificationTokens
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(t => t.TokenHash == hash, cancellationToken)
            .ConfigureAwait(false);

        if (token is null || !token.IsUsable(clock.UtcNow))
        {
            return Result.Failure(ErrorCodes.AuthVerificationInvalid, ErrorCodes.AuthVerificationInvalid);
        }

        tenantContext.Set(token.TenantId, token.UserId);
        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == token.UserId, cancellationToken)
            .ConfigureAwait(false);

        if (user is null)
        {
            return Result.Failure(ErrorCodes.AuthVerificationInvalid, ErrorCodes.AuthVerificationInvalid);
        }

        user.MarkEmailConfirmed(clock);
        token.Consume(clock.UtcNow);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }
}
