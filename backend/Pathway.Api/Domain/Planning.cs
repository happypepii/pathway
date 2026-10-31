namespace Pathway.Api.Domain;

public sealed class PlanningAnswer
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GoalId { get; set; }
    public Goal Goal { get; set; } = null!;
    public required string QuestionKey { get; set; }
    public required string Question { get; set; }
    public string? Answer { get; set; }
}

public sealed class Milestone
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GoalId { get; set; }
    public Goal Goal { get; set; } = null!;
    public required string Title { get; set; }
    public string? Description { get; set; }
    public string? Outcome { get; set; }
    public string ResourcesJson { get; set; } = "[]";
    public int SortOrder { get; set; }
    public List<PlanTask> Tasks { get; set; } = [];
}

public sealed class PlanTask
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MilestoneId { get; set; }
    public Milestone Milestone { get; set; } = null!;
    public required string Title { get; set; }
    public string? Description { get; set; }
    public string[] KeyPoints { get; set; } = [];
    public int EstimatedMinutes { get; set; }
    public DateOnly? ScheduledStartDate { get; set; }
    public DateOnly? ScheduledEndDate { get; set; }
    public int SortOrder { get; set; }
    public DateTime? CompletedAt { get; set; }
}
