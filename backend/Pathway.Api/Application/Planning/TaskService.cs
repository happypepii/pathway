using Microsoft.EntityFrameworkCore;
using Pathway.Api.Domain;
using Pathway.Api.Infrastructure.Persistence;

namespace Pathway.Api.Application.Planning;

public sealed class TaskService(PathwayDbContext database, TimeProvider clock)
{
    public async Task SetCompletion(Guid owner, Guid id, bool completed, CancellationToken ct)
    {
        var task = await database.Tasks.Include(x => x.Milestone).ThenInclude(x => x.Goal)
            .SingleOrDefaultAsync(x => x.Id == id && x.Milestone.Goal.SessionId == owner, ct)
            ?? throw new UseCaseException(404, "Task not found.");
        var goal = task.Milestone.Goal;
        task.CompletedAt = completed ? task.CompletedAt ?? clock.GetUtcNow().UtcDateTime : null;
        var otherUnfinished = await database.Tasks.AnyAsync(x => x.Milestone.GoalId == goal.Id &&
            x.Id != id && x.CompletedAt == null, ct);
        goal.Status = completed && !otherUnfinished ? GoalStatus.Completed : GoalStatus.Active;
        goal.UpdatedAt = clock.GetUtcNow().UtcDateTime;
        goal.Version = Guid.NewGuid();
        await database.SaveChangesAsync(ct);
    }

    public async Task Rename(Guid owner, Guid id, string title, CancellationToken ct)
    {
        var task = await database.Tasks.Include(x => x.Milestone).ThenInclude(x => x.Goal)
            .SingleOrDefaultAsync(x => x.Id == id && x.Milestone.Goal.SessionId == owner, ct)
            ?? throw new UseCaseException(404, "Task not found.");
        task.Title = title.Trim();
        task.Milestone.Goal.UpdatedAt = clock.GetUtcNow().UtcDateTime;
        task.Milestone.Goal.Version = Guid.NewGuid();
        await database.SaveChangesAsync(ct);
    }
}
