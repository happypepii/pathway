using Pathway.Api.Application.Sessions;
using Pathway.Api.Infrastructure.Sessions;

namespace Pathway.Api.Tests.Sessions;

public sealed class AnonymousSessionPolicyTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 30, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Creation_sets_utc_timestamps_and_fixed_30_day_expiration()
    {
        var policy = new AnonymousSessionPolicy(new TestTimeProvider(Now));
        var hash = SessionToken.Hash(SessionToken.Generate());
        var session = policy.Create(hash);

        Assert.Equal(hash, session.TokenHash);
        Assert.Equal(Now.UtcDateTime, session.CreatedAt);
        Assert.Equal(Now.UtcDateTime, session.LastSeenAt);
        Assert.Equal(Now.AddDays(30).UtcDateTime, session.ExpiresAt);
        Assert.Equal(DateTimeKind.Utc, session.ExpiresAt.Kind);
    }

    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, true)]
    [InlineData(1, true)]
    public void Expiration_is_inclusive_and_does_not_slide(long ticksFromExpiry, bool expired)
    {
        var clock = new TestTimeProvider(Now);
        var policy = new AnonymousSessionPolicy(clock);
        var session = policy.Create(SessionToken.Hash("test-token"));
        var originalExpiry = session.ExpiresAt;

        session.LastSeenAt = Now.AddDays(29).UtcDateTime;
        clock.Now = Now.AddDays(30).AddTicks(ticksFromExpiry);

        Assert.Equal(expired, policy.IsExpired(session));
        Assert.Equal(originalExpiry, session.ExpiresAt);
    }

    private sealed class TestTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
