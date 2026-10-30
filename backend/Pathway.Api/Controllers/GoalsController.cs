using Microsoft.AspNetCore.Mvc;
using Pathway.Api.Application.Goals;
using Pathway.Api.Application.Planning;
using Pathway.Api.Application.Sessions;
using Pathway.Api.Domain;

namespace Pathway.Api.Controllers;

[ApiController]
[Route("api/v1/goals")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class GoalsController(AnonymousSessionService sessions, GoalService goals, PlanningService plans, TimeProvider clock)
    : SessionControllerBase(sessions)
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        if (await SessionId(ct) is not Guid owner) return Unauthorized();
        return Ok((await goals.ListAsync(owner, ct)).Select(PlanningService.View));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        if (await SessionId(ct) is not Guid owner) return Unauthorized();
        return Ok(await plans.Detail(owner, id, ct));
    }

    [HttpPost]
    [RequestSizeLimit(16_384)]
    public async Task<IActionResult> Create(GoalInput input, CancellationToken ct)
    {
        if (await SessionId(ct) is not Guid owner) return Unauthorized();
        var errors = GoalValidation.Validate(input, clock.GetUtcNow());
        if (errors.Count > 0) return BadRequest(new ValidationProblemDetails(errors));
        var goal = await goals.CreateAsync(owner, input, ct);
        return CreatedAtAction(nameof(Get), new { id = goal.Id }, GoalService.View(goal));
    }

    [HttpPatch("{id:guid}")]
    [RequestSizeLimit(16_384)]
    public async Task<IActionResult> Update(Guid id, GoalInput input, CancellationToken ct)
    {
        if (await SessionId(ct) is not Guid owner) return Unauthorized();
        var goal = await goals.FindAsync(owner, id, ct);
        if (goal is null) return NotFound();
        if (goal.Status != GoalStatus.Draft &&
            (goal.TargetDate != input.TargetDate || goal.AvailableHoursPerWeek != input.AvailableHoursPerWeek ||
             goal.TimeZoneId != input.TimeZoneId))
            return Conflict(new ProblemDetails { Detail = "An activated plan keeps its original date, timezone and capacity." });
        var errors = GoalValidation.Validate(input, clock.GetUtcNow());
        // Existing activated goals may be past their target date; metadata edits remain possible.
        if (goal.Status != GoalStatus.Draft) errors.Remove("targetDate");
        if (errors.Count > 0) return BadRequest(new ValidationProblemDetails(errors));
        await goals.UpdateAsync(goal, input, ct);
        return Ok(GoalService.View(goal));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        if (await SessionId(ct) is not Guid owner) return Unauthorized();
        var goal = await goals.FindAsync(owner, id, ct);
        if (goal is null) return NotFound();
        await goals.DeleteAsync(goal, ct);
        return NoContent();
    }
}
