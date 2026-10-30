using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Pathway.Api.Application.Planning;
using Pathway.Api.Application.Sessions;

namespace Pathway.Api.Controllers;

public sealed record CompletionInput([Required] bool? Completed);
public sealed record RenameTaskInput([Required] string Title);

[ApiController]
[Route("api/v1/tasks")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class TasksController(AnonymousSessionService sessions, TaskService tasks) : SessionControllerBase(sessions)
{
    [HttpPatch("{id:guid}/completion")]
    public async Task<IActionResult> Completion(Guid id, CompletionInput input, CancellationToken ct)
    {
        if (await SessionId(ct) is not Guid owner) return Unauthorized();
        await tasks.SetCompletion(owner, id, input.Completed!.Value, ct);
        return NoContent();
    }

    [HttpPatch("{id:guid}")]
    [RequestSizeLimit(4096)]
    public async Task<IActionResult> Rename(Guid id, RenameTaskInput input, CancellationToken ct)
    {
        if (await SessionId(ct) is not Guid owner) return Unauthorized();
        if (!PlanningValidation.Text(input.Title, 200))
            return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]> { ["title"] = ["Provide 1–200 characters."] }));
        await tasks.Rename(owner, id, input.Title, ct);
        return NoContent();
    }
}
