namespace Pathway.Api.Domain;

public enum GoalStatus { Draft, Active, Completed, Archived }

public sealed class Goal
{
    public Guid Id { get; set; } = Guid.NewGuid();
    // Foreign key
    public Guid SessionId { get; set; } 
    // Navigation property to acess correspond Session Entity
    public AnonymousSession Session { get; set; } = null!;
    public required string Title { get; set; }
    public string? Description { get; set; }
    public DateOnly TargetDate { get; set; }
    public int AvailableHoursPerWeek { get; set; }
    public GoalStatus Status { get; set; } = GoalStatus.Draft;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
