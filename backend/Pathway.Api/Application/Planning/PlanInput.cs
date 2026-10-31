using System.ComponentModel.DataAnnotations;

namespace Pathway.Api.Application.Planning;

public sealed record ResourceInput(string Name, string Purpose, string SearchQuery);
public sealed record TaskInput([Required] string Title, [Range(1, 1000)] int EstimatedHours,
    string? Description = null, List<string>? KeyPoints = null);
public sealed record MilestoneInput([Required] string Title, [Required] List<TaskInput> Tasks,
    string? Description = null, string? Outcome = null, List<ResourceInput>? Resources = null);
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

    public static Dictionary<string, string[]> Validate(PlanInput? input, bool requireGuidance = false)
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
            if (!OptionalText(milestone.Description, 1000) || !OptionalText(milestone.Outcome, 500) ||
                milestone.Resources?.Count > 2 || milestone.Resources?.Any(r => r is null ||
                    !ResourceText(r.Name, 200) || !ResourceText(r.Purpose, 400) || !ResourceText(r.SearchQuery, 200)) == true ||
                requireGuidance && (milestone.Description is null || milestone.Outcome is null || milestone.Resources is not { Count: >= 1 }))
                errors["guidance"] = ["Stages need bounded descriptions, outcomes and up to two resource suggestions without URLs."];
            count += milestone.Tasks.Count;
            foreach (var task in milestone.Tasks)
            {
                if (task is null || !Text(task.Title, 200) || task.EstimatedHours is < 1 or > 1000)
                    errors["tasks"] = ["Tasks need titles of 1–200 characters and 1–1,000 whole hours."];
                else if (!OptionalText(task.Description, 2000) || task.KeyPoints?.Count > 3 ||
                    task.KeyPoints?.Any(p => !Text(p, 200)) == true ||
                    requireGuidance && (task.Description is null || task.KeyPoints is not { Count: >= 1 }))
                    errors["guidance"] = ["Task descriptions are limited to 2,000 characters and up to three nonempty key points of 200 characters each."];
            }
        }
        if (count > 100) errors["tasks"] = ["A plan can contain at most 100 tasks."];
        return errors;
    }

    public static bool Text(string? value, int limit) =>
        !string.IsNullOrWhiteSpace(value) && value.Trim().Length <= limit && !value.Contains('\0');

    private static bool OptionalText(string? value, int limit) => value is null || Text(value, limit);
    private static bool ResourceText(string? value, int limit) => Text(value, limit) &&
        !new[] { "http://", "https://", "www." }.Any(prefix => value!.Contains(prefix, StringComparison.OrdinalIgnoreCase));

    public static Dictionary<string, string[]> Validate(AnswerInput input) =>
        new[] { input.Experience, input.Outcome, input.Constraints }.Any(x => x?.Length > 2000 || x?.Contains('\0') == true)
            ? new() { ["answers"] = ["Each optional answer must contain at most 2,000 characters."] } : [];
}
