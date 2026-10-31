using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Pathway.Api.Application.Goals;
using Pathway.Api.Domain;
using Pathway.Api.Infrastructure.Persistence;

namespace Pathway.Api.Application.Planning;

public sealed record ConfirmationInput(List<MilestoneInput> Milestones, Guid GoalVersion);

public sealed class PlanningService(PathwayDbContext database, TimeProvider clock, IPlanGenerator generator)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private async Task<Goal> Owned(Guid owner, Guid id, CancellationToken ct) =>
        await database.Goals.Include(x => x.PlanningAnswers).Include(x => x.Milestones).ThenInclude(x => x.Tasks)
            .AsSplitQuery().SingleOrDefaultAsync(x => x.Id == id && x.SessionId == owner, ct)
        ?? throw new UseCaseException(404, "Goal not found.");

    private static void Draft(Goal goal)
    {
        if (goal.Status != GoalStatus.Draft)
            throw new UseCaseException(409, "An activated plan cannot be regenerated or replaced.");
    }

    private DateOnly Today(Goal goal) => DateOnly.FromDateTime(
        TimeZoneInfo.ConvertTime(clock.GetUtcNow(), GoalValidation.ResolveTimeZone(goal.TimeZoneId)!).DateTime);

    private void Changed(Goal goal)
    {
        goal.UpdatedAt = clock.GetUtcNow().UtcDateTime;
        goal.Version = Guid.NewGuid();
    }

    public async Task<object> Detail(Guid owner, Guid id, CancellationToken ct) => View(await Owned(owner, id, ct));

    public async Task SaveAnswers(Guid owner, Guid id, AnswerInput input, CancellationToken ct)
    {
        var goal = await Owned(owner, id, ct);
        Draft(goal);
        var values = new[] { input.Experience, input.Outcome, input.Constraints };
        for (var i = 0; i < PlanningValidation.Questions.Length; i++)
        {
            var question = PlanningValidation.Questions[i];
            var answer = goal.PlanningAnswers.SingleOrDefault(x => x.QuestionKey == question.Key);
            if (answer is null)
            {
                answer = new PlanningAnswer { GoalId = id, QuestionKey = question.Key, Question = question.Text };
                goal.PlanningAnswers.Add(answer);
            }
            answer.Answer = string.IsNullOrWhiteSpace(values[i]) ? null : values[i]!.Trim();
        }
        Changed(goal);
        await database.SaveChangesAsync(ct);
    }

    public async Task<object> Preview(Guid owner, Guid id, CancellationToken ct)
    {
        var goal = await Owned(owner, id, ct);
        Draft(goal);
        string? Answer(string key) => goal.PlanningAnswers.SingleOrDefault(x => x.QuestionKey == key)?.Answer;
        PlanInput plan;
        try
        {
            plan = await generator.GenerateAsync(new(goal.Title, goal.Description,
                new(Answer("experience"), Answer("outcome"), Answer("constraints"))), ct);
        }
        catch (PlanningUnavailableException) { throw new UseCaseException(503, "規劃服務暫時無法使用，請稍後重試。"); }
        catch (PlanningOutputException) { throw new UseCaseException(502, "規劃服務傳回無效內容，請重試。"); }
        if (PlanningValidation.Validate(plan).Count > 0)
            throw new UseCaseException(502, "規劃服務傳回無效內容，請重試。");
        var preview = SequentialScheduler.Schedule(plan, goal.AvailableHoursPerWeek, Today(goal), goal.TargetDate);
        return new { preview.Milestones, preview.Schedule, GoalVersion = goal.Version };
    }

    public async Task<object> Confirm(Guid owner, Guid id, ConfirmationInput input, CancellationToken ct)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(ct);
        // Serialize confirmation against other writers before loading the current version.
        await database.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM goals WHERE id = {id} AND session_id = {owner} FOR UPDATE", ct);
        var goal = await Owned(owner, id, ct);
        Draft(goal);
        if (input.GoalVersion != goal.Version)
            throw new UseCaseException(409, "規劃條件已變更，請重新產生預覽。");
        var plan = new PlanInput(input.Milestones);
        var preview = SequentialScheduler.Schedule(plan, goal.AvailableHoursPerWeek, Today(goal), goal.TargetDate);
        for (var m = 0; m < preview.Milestones.Count; m++)
        {
            var source = preview.Milestones[m];
            var milestone = new Milestone { GoalId = id, Title = source.Title, SortOrder = m,
                Description = source.Description, Outcome = source.Outcome,
                ResourcesJson = JsonSerializer.Serialize(source.Resources ?? [], Json) };
            for (var t = 0; t < source.Tasks.Count; t++)
            {
                var task = source.Tasks[t];
                milestone.Tasks.Add(new PlanTask { Title = task.Title, EstimatedMinutes = task.EstimatedHours * 60,
                    SortOrder = t, ScheduledStartDate = task.ScheduledStartDate, ScheduledEndDate = task.ScheduledEndDate,
                    Description = task.Description, KeyPoints = task.KeyPoints?.ToArray() ?? [] });
            }
            goal.Milestones.Add(milestone);
        }
        goal.Status = GoalStatus.Active;
        Changed(goal);
        await database.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return View(goal);
    }

    public static object View(Goal goal)
    {
        var tasks = goal.Milestones.OrderBy(x => x.SortOrder).SelectMany(x => x.Tasks.OrderBy(t => t.SortOrder)).ToList();
        var completed = tasks.Count(x => x.CompletedAt is not null);
        var next = tasks.FirstOrDefault(x => x.CompletedAt is null);
        return new
        {
            goal.Id, goal.Title, goal.Description, goal.TargetDate, goal.AvailableHoursPerWeek, goal.TimeZoneId,
            Status = goal.Status.ToString(), GoalVersion = goal.Version, goal.CreatedAt, goal.UpdatedAt,
            Progress = tasks.Count == 0 ? 0 : (int)Math.Round(100.0 * completed / tasks.Count),
            TotalTasks = tasks.Count, CompletedTasks = completed,
            NextTask = next is null ? null : new { next.Id, next.Title, next.ScheduledStartDate, next.ScheduledEndDate },
            Schedule = tasks.Count == 0 ? null : new ScheduleSummary(tasks.Sum(t => t.EstimatedMinutes) / 60,
                tasks.Max(t => t.ScheduledEndDate) <= goal.TargetDate, tasks.Min(t => t.ScheduledStartDate)!.Value,
                tasks.Max(t => t.ScheduledEndDate)!.Value,
                tasks.Max(t => t.ScheduledEndDate) <= goal.TargetDate ? [] : ["計畫預計完成日超過目標日期；時數估計未被縮減。"]),
            Questions = PlanningValidation.Questions,
            Answers = goal.PlanningAnswers.ToDictionary(x => x.QuestionKey, x => x.Answer),
            Milestones = goal.Milestones.OrderBy(x => x.SortOrder).Select(m => new
            {
                m.Id, m.Title, m.Description, m.Outcome,
                Resources = JsonSerializer.Deserialize<List<ResourceInput>>(m.ResourcesJson, Json) ?? [],
                Tasks = m.Tasks.OrderBy(x => x.SortOrder).Select(t => new
                {
                    t.Id, t.Title, t.Description, t.KeyPoints, EstimatedHours = t.EstimatedMinutes / 60,
                    t.ScheduledStartDate, t.ScheduledEndDate, Completed = t.CompletedAt is not null
                })
            })
        };
    }
}
