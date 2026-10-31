using Pathway.Api.Application.Planning;
using Pathway.Api.Domain;

namespace Pathway.Api.Tests.Planning;

public sealed class GuidanceValidationTests
{
    internal static PlanInput GuidedPlan() => new([new("Milestone", [new("Task", 2,
        "Follow the instructions and create a small example.", ["Check the result."])],
        "Build a practical foundation.", "A working first example.",
        [new("Beginner guide", "Learn the basic steps.", "beginner practical guide")])]);

    [Fact]
    public void New_generation_requires_guidance_but_legacy_confirmation_remains_valid()
    {
        var legacy = SchedulerTests.Plan(2);
        Assert.Empty(PlanningValidation.Validate(legacy));
        Assert.NotEmpty(PlanningValidation.Validate(legacy, requireGuidance: true));
        Assert.Empty(PlanningValidation.Validate(GuidedPlan(), requireGuidance: true));
    }

    [Theory]
    [InlineData("stage-description")]
    [InlineData("outcome")]
    [InlineData("task-description")]
    [InlineData("points-count")]
    [InlineData("point-length")]
    [InlineData("empty-point")]
    [InlineData("resources-count")]
    [InlineData("resource-name")]
    [InlineData("resource-purpose")]
    [InlineData("resource-query")]
    [InlineData("resource-url")]
    [InlineData("null-resource")]
    public void Invalid_guidance_is_rejected_before_persistence(string scenario)
    {
        var plan = GuidedPlan();
        var stage = plan.Milestones[0];
        var task = stage.Tasks[0];
        var resource = stage.Resources![0];
        stage = scenario switch
        {
            "stage-description" => stage with { Description = new string('x', 1001) },
            "outcome" => stage with { Outcome = new string('x', 501) },
            "resources-count" => stage with { Resources = [resource, resource, resource] },
            "resource-name" => stage with { Resources = [resource with { Name = new string('x', 201) }] },
            "resource-purpose" => stage with { Resources = [resource with { Purpose = new string('x', 401) }] },
            "resource-query" => stage with { Resources = [resource with { SearchQuery = new string('x', 201) }] },
            "resource-url" => stage with { Resources = [resource with { SearchQuery = "https://unverified.invalid/guide" }] },
            "null-resource" => stage with { Resources = [null!] },
            _ => stage
        };
        task = scenario switch
        {
            "task-description" => task with { Description = new string('x', 2001) },
            "points-count" => task with { KeyPoints = ["a", "b", "c", "d"] },
            "point-length" => task with { KeyPoints = [new string('x', 201)] },
            "empty-point" => task with { KeyPoints = [" "] },
            _ => task
        };
        plan.Milestones[0] = stage with { Tasks = [task] };
        Assert.NotEmpty(PlanningValidation.Validate(plan));
    }

    [Fact]
    public void Scheduling_preserves_guidance_without_changing_dates_or_effort()
    {
        var start = new DateOnly(2026, 10, 31);
        var guided = SequentialScheduler.Schedule(GuidedPlan(), 7, start, start.AddDays(20));
        var legacy = SequentialScheduler.Schedule(SchedulerTests.Plan(2), 7, start, start.AddDays(20));
        Assert.Equal(legacy.Schedule, guided.Schedule);
        Assert.Equal(GuidedPlan().Milestones[0].Description, guided.Milestones[0].Description);
        Assert.Equal(GuidedPlan().Milestones[0].Resources, guided.Milestones[0].Resources);
        Assert.Equal(GuidedPlan().Milestones[0].Tasks[0].KeyPoints, guided.Milestones[0].Tasks[0].KeyPoints);
        Assert.Equal(legacy.Milestones[0].Tasks[0].ScheduledEndDate, guided.Milestones[0].Tasks[0].ScheduledEndDate);
    }
}
