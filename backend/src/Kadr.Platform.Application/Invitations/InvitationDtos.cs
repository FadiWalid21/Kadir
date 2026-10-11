namespace Kadr.Platform.Application.Invitations;

public sealed record InviteUserRequest(string Email, Guid RoleId);

public sealed record InvitationListItemDto(
    Guid Id,
    string Email,
    Guid RoleId,
    string RoleName,
    DateTimeOffset ExpiresAt,
    DateTimeOffset CreatedAt);

public sealed record ValidateInvitationRequest(string Token);

public sealed record ValidateInvitationResponse(
    string Status,
    string? Email,
    string? CompanyName,
    string? RoleName);

public sealed record AcceptInvitationRequest(string Token, string FullName, string Password);
