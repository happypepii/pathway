using System.ComponentModel.DataAnnotations;

namespace Pathway.Api.Application.Goals;

public sealed record GoalInput(
    [Required] string Title,
    string? Description,
    DateOnly TargetDate,
    [Range(1, 168)] int AvailableHoursPerWeek,
    [Required] string TimeZoneId);

public static class GoalValidation
{
    public static Dictionary<string, string[]> Validate(GoalInput input, DateTimeOffset now)
    {
        var errors = new Dictionary<string, string[]>();
        var title = input.Title?.Trim();
        if (string.IsNullOrEmpty(title) || title.Length > 200 || title.Contains('\0'))
            errors["title"] = ["Title must contain 1–200 characters after trimming."];
        if (input.Description?.Length > 2000 || input.Description?.Contains('\0') == true)
            errors["description"] = ["Description must contain at most 2,000 characters."];
        if (input.AvailableHoursPerWeek is < 1 or > 168)
            errors["availableHoursPerWeek"] = ["Weekly hours must be an integer from 1 to 168."];
        var zone = ResolveTimeZone(input.TimeZoneId);
        if (zone is null)
            errors["timeZoneId"] = ["Provide a valid IANA timezone."];
        else if (input.TargetDate < DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone).DateTime))
            errors["targetDate"] = ["Target date must be today or later in your timezone."];
        return errors;
    }

    public static TimeZoneInfo? ResolveTimeZone(string? id)
    {
        if (string.IsNullOrWhiteSpace(id) || id.Length > 100 ||
            (id != "UTC" && !TimeZoneInfo.TryConvertIanaIdToWindowsId(id, out _))) return null;
        try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
        catch (TimeZoneNotFoundException) { return null; }
        catch (InvalidTimeZoneException) { return null; }
    }
}
