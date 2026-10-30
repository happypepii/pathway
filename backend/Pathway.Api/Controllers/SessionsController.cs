using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Pathway.Api.Infrastructure.Sessions;
using Pathway.Api.Application.Sessions;

namespace Pathway.Api.Controllers;

[ApiController]
[Route("api/v1/sessions")]
public sealed class SessionsController(AnonymousSessionService sessions) : ControllerBase
{
    [HttpPost("anonymous")]
    [EnableRateLimiting("session-creation")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Anonymous(CancellationToken cancellationToken)
    {
        var existing = await sessions.ResolveAsync(
            Request.Cookies[SessionCookieName], cancellationToken);
        if (existing is not null)
            return Ok();

        var token = SessionToken.Generate();
        var session = await sessions.CreateAsync(token, cancellationToken);
        Response.Cookies.Append(SessionCookieName, token, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Path = "/",
            Expires = new DateTimeOffset(session.ExpiresAt)
        });
        return StatusCode(StatusCodes.Status201Created);
    }

    public const string SessionCookieName = "__Host-pathway_session";

    [HttpGet("me")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Me(CancellationToken cancellationToken)
    {
        var session = await sessions.ResolveAsync(
            Request.Cookies[SessionCookieName], cancellationToken);

        return session is null ? Unauthorized() : Ok();
    }
}
