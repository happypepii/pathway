using Microsoft.AspNetCore.Mvc;
using Pathway.Api.Application.Sessions;

namespace Pathway.Api.Controllers;

[ApiController]
[Route("api/v1/sessions")]
public sealed class SessionsController(AnonymousSessionService sessions) : ControllerBase
{
    [HttpGet("me")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Me(CancellationToken cancellationToken)
    {
        var session = await sessions.ResolveAsync(
            Request.Cookies["__Host-pathway_session"], cancellationToken);

        return session is null ? Unauthorized() : Ok();
    }
}
