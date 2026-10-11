using Kadr.Platform.Application.Invitations;
using Kadr.SharedKernel.Results;

namespace Kadr.Platform.Application.Abstractions;

public interface IInvitationService
{
    Task<Result<InvitationListItemDto>> InviteAsync(
        InviteUserRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<InvitationListItemDto>>> ListPendingAsync(
        CancellationToken cancellationToken = default);

    Task<Result<InvitationListItemDto>> ResendAsync(
        Guid id,
        Guid? roleId,
        CancellationToken cancellationToken = default);

    Task<Result> RevokeAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Result<ValidateInvitationResponse>> ValidateAsync(
        string token,
        CancellationToken cancellationToken = default);

    Task<Result> AcceptAsync(
        AcceptInvitationRequest request,
        CancellationToken cancellationToken = default);
}
