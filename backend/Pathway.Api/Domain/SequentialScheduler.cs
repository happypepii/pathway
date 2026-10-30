using Pathway.Api.Application.Planning;

namespace Pathway.Api.Domain;

public sealed record ScheduledTask(string Title, int EstimatedHours, DateOnly ScheduledStartDate, DateOnly ScheduledEndDate);
public sealed record ScheduledMilestone(string Title, List<ScheduledTask> Tasks);
public sealed record ScheduleSummary(int TotalEstimatedHours, bool IsFeasible, DateOnly StartDate, DateOnly EndDate, string[] Warnings);
public sealed record PlanPreview(List<ScheduledMilestone> Milestones, ScheduleSummary Schedule);

public static class SequentialScheduler
{
    // Integer rational arithmetic: weekly whole hours are spread evenly over seven days.
    // Work offsets use seventh-hours, avoiding daily rounding and capacity over-allocation.
    public static PlanPreview Schedule(PlanInput plan, int weeklyHours, DateOnly start, DateOnly target)
    {
        if (PlanningValidation.Validate(plan).Count > 0 || weeklyHours is < 1 or > 168)
            throw new ArgumentException("Invalid scheduling input.");
        var total = 0;
        var milestones = new List<ScheduledMilestone>();
        foreach (var milestone in plan.Milestones)
        {
            var tasks = new List<ScheduledTask>();
            foreach (var task in milestone.Tasks)
            {
                var begins = start.AddDays(total * 7 / weeklyHours);
                total += task.EstimatedHours;
                var ends = start.AddDays((total * 7 - 1) / weeklyHours);
                tasks.Add(new(task.Title.Trim(), task.EstimatedHours, begins, ends));
            }
            milestones.Add(new(milestone.Title.Trim(), tasks));
        }
        var end = milestones[^1].Tasks[^1].ScheduledEndDate;
        var feasible = end <= target;
        return new(milestones, new(total, feasible, start, end,
            feasible ? [] : ["依目前估計，計畫將晚於目標日期完成。你可以減少工作量、增加每週時數，或延後目標日期。"]));
    }
}
