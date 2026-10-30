using Pathway.Api.Application.Planning;
using Pathway.Api.Domain;

namespace Pathway.Api.Tests.Planning;

public sealed class SchedulerTests
{
    private static readonly DateOnly Start = new(2026, 10, 30);
    internal static PlanInput Plan(params int[] hours) => new([new(" Milestone ",
        hours.Select((h, i) => new TaskInput($" Task {i} ", h)).ToList())]);

    [Theory]
    [InlineData(7, 1, 0)]
    [InlineData(1, 1, 6)]
    [InlineData(10, 4, 2)]
    [InlineData(168, 24, 0)]
    [InlineData(168, 25, 1)]
    public void End_dates_use_exact_fractional_daily_capacity(int capacity, int hours, int endDay)
    {
        var result = SequentialScheduler.Schedule(Plan(hours), capacity, Start, Start);
        Assert.Equal(Start.AddDays(endDay), result.Schedule.EndDate);
        Assert.Equal(hours, result.Schedule.TotalEstimatedHours);
        Assert.Equal(endDay == 0, result.Schedule.IsFeasible);
    }

    [Fact]
    public void Tasks_share_day_capacity_and_can_span_days_across_milestones()
    {
        var input = new PlanInput([new("First", [new("A", 1)]), new("Second", [new("B", 2)])]);
        var result = SequentialScheduler.Schedule(input, 10, Start, Start.AddDays(1));
        var second = result.Milestones[1].Tasks[0];
        Assert.Equal(Start, second.ScheduledStartDate);
        Assert.Equal(Start.AddDays(2), second.ScheduledEndDate);
        Assert.False(result.Schedule.IsFeasible);
        Assert.Single(result.Schedule.Warnings);
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(result),
            System.Text.Json.JsonSerializer.Serialize(SequentialScheduler.Schedule(input, 10, Start, Start.AddDays(1))));
    }

    [Fact]
    public void Many_tasks_never_reset_daily_capacity_or_accumulate_rounding_errors()
    {
        foreach (var capacity in Enumerable.Range(1, 168))
        {
            var result = SequentialScheduler.Schedule(Plan(Enumerable.Repeat(1, 100).ToArray()), capacity, Start, Start.AddDays(1000));
            for (var i = 0; i < 100; i++)
            {
                Assert.Equal(Start.AddDays(i * 7 / capacity), result.Milestones[0].Tasks[i].ScheduledStartDate);
                Assert.Equal(Start.AddDays(((i + 1) * 7 - 1) / capacity), result.Milestones[0].Tasks[i].ScheduledEndDate);
            }
        }
    }

    [Fact]
    public void Invalid_and_excessive_plans_are_rejected()
    {
        Assert.NotEmpty(PlanningValidation.Validate(Plan(0)));
        Assert.NotEmpty(PlanningValidation.Validate(Plan(1001)));
        Assert.NotEmpty(PlanningValidation.Validate(Plan(Enumerable.Repeat(1, 101).ToArray())));
        Assert.NotEmpty(PlanningValidation.Validate(new PlanInput([])));
        Assert.NotEmpty(PlanningValidation.Validate(new PlanInput([null!])));
        Assert.NotEmpty(PlanningValidation.Validate(new PlanInput([new("ok", [null!])])));
        Assert.Throws<ArgumentException>(() => SequentialScheduler.Schedule(Plan(1), 0, Start, Start));
    }
}
