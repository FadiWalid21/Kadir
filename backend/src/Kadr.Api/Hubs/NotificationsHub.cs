using System.Security.Claims;
using Kadr.SharedKernel.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Kadr.Api.Hubs;

[Authorize]
public sealed class NotificationsHub : Hub
{
    public static string UserGroup(Guid userId) => $"user:{userId:D}";

    public override async Task OnConnectedAsync()
    {
        var userId = ResolveUserId(Context.User);
        if (userId is null)
        {
            Context.Abort();
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, UserGroup(userId.Value)).ConfigureAwait(false);
        await base.OnConnectedAsync().ConfigureAwait(false);
    }

    private static Guid? ResolveUserId(ClaimsPrincipal? user)
    {
        var raw = user?.FindFirstValue(ClaimNames.Subject)
            ?? user?.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(raw, out var id) && id != Guid.Empty ? id : null;
    }
}
