using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Pathway.Api.Application.Planning;
using Pathway.Api.Application.Sessions;

namespace Pathway.Api.Controllers;

[ApiController]
[Route("api/v1/goals/{id:guid}")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[RequestSizeLimit(131_072)]
public sealed class PlanningController(AnonymousSessionService sessions, PlanningService plans)
    : SessionControllerBase(sessions)
{
    [HttpPut("planning-answers")]
    public async Task<IActionResult> Answers(Guid id, AnswerInput input, CancellationToken ct)
    {
        if (await SessionId(ct) is not Guid owner) return Unauthorized();
        var errors = PlanningValidation.Validate(input);
        if (errors.Count > 0) return BadRequest(new ValidationProblemDetails(errors));
        await plans.SaveAnswers(owner, id, input, ct);
        return NoContent();
    }

    [HttpPost("plan-preview")]
    [EnableRateLimiting("planning")]
    public async Task<IActionResult> Preview(Guid id, CancellationToken ct)
    {
        if (await SessionId(ct) is not Guid owner) return Unauthorized();
        return Ok(await plans.Preview(owner, id, ct));
    }

    [HttpPost("plan")]
    public async Task<IActionResult> Confirm(Guid id, ConfirmationInput input, CancellationToken ct)
    {
        if (await SessionId(ct) is not Guid owner) return Unauthorized();
        var errors = PlanningValidation.Validate(new PlanInput(input.Milestones));
        if (errors.Count > 0) return BadRequest(new ValidationProblemDetails(errors));
        return Ok(await plans.Confirm(owner, id, input, ct));
    }
}
