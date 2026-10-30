using Microsoft.EntityFrameworkCore;
using Pathway.Api.Domain;
using Pathway.Api.Infrastructure.Persistence;

namespace Pathway.Api.Application.Goals;

public sealed class GoalService(PathwayDbContext database, TimeProvider clock)
{
    public async Task<Goal> CreateAsync(Guid sessionId, GoalInput input, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var goal = new Goal { SessionId = sessionId, Title = input.Title.Trim(),
            Description = input.Description, TargetDate = input.TargetDate,
            AvailableHoursPerWeek = input.AvailableHoursPerWeek, TimeZoneId = input.TimeZoneId,
            CreatedAt = now, UpdatedAt = now };
        database.Goals.Add(goal);
        await database.SaveChangesAsync(ct);
        return goal;
    }

    public Task<List<Goal>> ListAsync(Guid sessionId, CancellationToken ct) =>
        database.Goals.AsNoTracking().Include(x => x.Milestones).ThenInclude(x => x.Tasks).AsSplitQuery().Where(x => x.SessionId == sessionId)
            .OrderByDescending(x => x.CreatedAt).ToListAsync(ct);

    public Task<Goal?> FindAsync(Guid sessionId, Guid id, CancellationToken ct) =>
        database.Goals.SingleOrDefaultAsync(x => x.SessionId == sessionId && x.Id == id, ct);

    public async Task UpdateAsync(Goal goal, GoalInput input, CancellationToken ct)
    {
        goal.Title = input.Title.Trim();
        goal.Description = input.Description;
        goal.TargetDate = input.TargetDate;
        goal.AvailableHoursPerWeek = input.AvailableHoursPerWeek;
        goal.TimeZoneId = input.TimeZoneId;
        goal.UpdatedAt = clock.GetUtcNow().UtcDateTime;
        goal.Version = Guid.NewGuid();
        await database.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Goal goal, CancellationToken ct)
    {
        database.Goals.Remove(goal);
        await database.SaveChangesAsync(ct);
    }

    public static object View(Goal goal) => new
    {
        goal.Id, goal.Title, goal.Description, goal.TargetDate,
        goal.AvailableHoursPerWeek, goal.TimeZoneId, Status = goal.Status.ToString(),
        goal.CreatedAt, goal.UpdatedAt
    };
}
