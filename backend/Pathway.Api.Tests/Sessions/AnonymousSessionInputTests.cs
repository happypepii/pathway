using Microsoft.EntityFrameworkCore;
using Pathway.Api.Application.Sessions;
using Pathway.Api.Infrastructure.Persistence;

namespace Pathway.Api.Tests.Sessions;

public sealed class AnonymousSessionInputTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("short")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA+")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=")]
    public async Task Malformed_tokens_are_rejected_before_database_access(string? token)
    {
        // No provider: any attempted database access would fail this test.
        await using var context = new PathwayDbContext(new DbContextOptions<PathwayDbContext>());
        var service = new AnonymousSessionService(context, new AnonymousSessionPolicy(TimeProvider.System));

        Assert.Null(await service.ResolveAsync(token));
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(token!));
    }
}
