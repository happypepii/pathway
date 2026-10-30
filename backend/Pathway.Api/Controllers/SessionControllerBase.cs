using Microsoft.AspNetCore.Mvc;
using Pathway.Api.Application.Sessions;

namespace Pathway.Api.Controllers;

public abstract class SessionControllerBase(AnonymousSessionService sessions) : ControllerBase
{
    protected async Task<Guid?> SessionId(CancellationToken ct) =>
        (await sessions.ResolveAsync(Request.Cookies[SessionsController.SessionCookieName], ct))?.Id;
}
