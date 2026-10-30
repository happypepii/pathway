using Microsoft.EntityFrameworkCore;
using Pathway.Api.Application.Sessions;
using Pathway.Api.Infrastructure.Persistence;
using Pathway.Api.Infrastructure.Sessions;
using Pathway.Api.Tests.Persistence;

namespace Pathway.Api.Tests.Sessions;

public sealed class AnonymousSessionServiceTests(PostgresFixture database) : IClassFixture<PostgresFixture>
{
    private static readonly DateTimeOffset Now = new(2026, 10, 30, 12, 0, 0, TimeSpan.Zero);

    private static AnonymousSessionService Service(PathwayDbContext context, DateTimeOffset now) =>
        new(context, new AnonymousSessionPolicy(new TestTimeProvider(now)));

    [Fact]
    public async Task Creation_persists_hash_and_resolution_reuses_session_without_writes()
    {
        var token = SessionToken.Generate();
        Guid id;
        await using (var writer = database.CreateContext())
        {
            id = (await Service(writer, Now).CreateAsync(token)).Id;
        }

        await using (var reader = database.CreateContext())
        {
            var service = Service(reader, Now.AddDays(29));
            var first = await service.ResolveAsync(token);
            var second = await service.ResolveAsync(token);
            Assert.NotNull(first);
            Assert.NotNull(second);
            Assert.Equal(id, first.Id);
            Assert.Equal(id, second.Id);
            Assert.Equal(SessionToken.Hash(token), first.TokenHash);
            Assert.NotEqual(token, first.TokenHash);
            Assert.Empty(reader.ChangeTracker.Entries());
        }

        await using var verify = database.CreateContext();
        var saved = await verify.AnonymousSessions.SingleAsync(x => x.Id == id);
        Assert.Equal(Now.UtcDateTime, saved.CreatedAt);
        Assert.Equal(Now.UtcDateTime, saved.LastSeenAt);
        Assert.Equal(Now.AddDays(30).UtcDateTime, saved.ExpiresAt);
    }

    [Theory]
    [InlineData(-10, false)]
    [InlineData(0, true)]
    [InlineData(10, true)]
    public async Task Resolution_checks_expiration_and_keeps_expired_row(long ticksFromExpiry, bool expired)
    {
        var token = SessionToken.Generate();
        Guid id;
        await using (var writer = database.CreateContext())
        {
            id = (await Service(writer, Now).CreateAsync(token)).Id;
        }

        await using var reader = database.CreateContext();
        var result = await Service(reader, Now.AddDays(30).AddTicks(ticksFromExpiry)).ResolveAsync(token);
        Assert.Equal(expired, result is null);
        Assert.True(await reader.AnonymousSessions.AnyAsync(x => x.Id == id));
    }

    [Fact]
    public async Task Unknown_token_does_not_create_session()
    {
        await using var context = database.CreateContext();
        var before = await context.AnonymousSessions.CountAsync();
        Assert.Null(await Service(context, Now).ResolveAsync(SessionToken.Generate()));
        Assert.Equal(before, await context.AnonymousSessions.CountAsync());
    }

    private sealed class TestTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
