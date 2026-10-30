using Pathway.Api.Application.Goals;

namespace Pathway.Api.Tests.Goals;

public sealed class GoalValidationTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 30, 0, 30, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("Asia/Taipei", "2026-10-30", true)]
    [InlineData("America/Los_Angeles", "2026-10-29", true)]
    [InlineData("Asia/Taipei", "2026-10-29", false)]
    [InlineData("Taipei Standard Time", "2026-10-30", false)]
    [InlineData("unknown", "2026-10-30", false)]
    public void Dates_use_valid_IANA_timezone(string zone, string date, bool valid)
    {
        var input = new GoalInput(" Test ", null, DateOnly.Parse(date), 1, zone);
        Assert.Equal(valid, GoalValidation.Validate(input, Now).Count == 0);
    }

    [Theory]
    [InlineData("", 1)]
    [InlineData("   ", 1)]
    [InlineData("goal", 0)]
    [InlineData("goal", 169)]
    public void Invalid_title_or_capacity_is_rejected(string title, int capacity) =>
        Assert.NotEmpty(GoalValidation.Validate(
            new GoalInput(title, null, new DateOnly(2026, 10, 30), capacity, "UTC"), Now));
}
