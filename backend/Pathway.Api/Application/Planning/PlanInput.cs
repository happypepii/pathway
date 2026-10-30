using System.ComponentModel.DataAnnotations;

namespace Pathway.Api.Application.Planning;

public sealed record TaskInput([Required] string Title, [Range(1, 1000)] int EstimatedHours);
public sealed record MilestoneInput([Required] string Title, [Required] List<TaskInput> Tasks);
public sealed record PlanInput([Required] List<MilestoneInput> Milestones);
public sealed record AnswerInput(string? Experience, string? Outcome, string? Constraints);
public sealed record Question(string Key, string Text);

public static class PlanningValidation
{
    public static readonly Question[] Questions =
    [
        new("experience", "你目前的經驗或起點是什麼？"),
        new("outcome", "完成時，你希望看到什麼具體成果？"),
        new("constraints", "有哪些限制、偏好或可使用的資源？")
    ];

    public static Dictionary<string, string[]> Validate(PlanInput? input)
    {
        var errors = new Dictionary<string, string[]>();
        if (input?.Milestones is not { Count: >= 1 and <= 10 })
        {
            errors["milestones"] = ["A plan requires 1–10 milestones."];
            return errors;
        }
        var count = 0;
        foreach (var milestone in input.Milestones)
        {
            if (milestone is null || !Text(milestone.Title, 200) || milestone.Tasks is not { Count: >= 1 and <= 100 })
            { errors["milestones"] = ["Each milestone needs a title and 1–100 tasks."]; continue; }
            count += milestone.Tasks.Count;
            foreach (var task in milestone.Tasks)
                if (task is null || !Text(task.Title, 200) || task.EstimatedHours is < 1 or > 1000)
                    errors["tasks"] = ["Tasks need titles of 1–200 characters and 1–1,000 whole hours."];
        }
        if (count > 100) errors["tasks"] = ["A plan can contain at most 100 tasks."];
        return errors;
    }

    public static bool Text(string? value, int limit) =>
        !string.IsNullOrWhiteSpace(value) && value.Trim().Length <= limit && !value.Contains('\0');

    public static Dictionary<string, string[]> Validate(AnswerInput input) =>
        new[] { input.Experience, input.Outcome, input.Constraints }.Any(x => x?.Length > 2000 || x?.Contains('\0') == true)
            ? new() { ["answers"] = ["Each optional answer must contain at most 2,000 characters."] } : [];
}
