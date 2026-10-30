using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Pathway.Api.Application.Sessions;
using Pathway.Api.Infrastructure.Persistence;
using Pathway.Api.Infrastructure.Sessions;
using Pathway.Api.Tests.Persistence;

namespace Pathway.Api.Tests.Sessions;

public sealed class SessionEndpointTests(PostgresFixture database) : IClassFixture<PostgresFixture>
{
    private static readonly DateTimeOffset Now = new(2026, 10, 30, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("valid", HttpStatusCode.OK)]
    [InlineData("missing", HttpStatusCode.Unauthorized)]
    [InlineData("malformed", HttpStatusCode.Unauthorized)]
    [InlineData("unknown", HttpStatusCode.Unauthorized)]
    [InlineData("expired", HttpStatusCode.Unauthorized)]
    public async Task Me_resolves_cookie_without_writes_or_credential_disclosure(string scenario, HttpStatusCode expected)
    {
        var token = SessionToken.Generate();
        var createdAt = scenario == "expired" ? Now.AddDays(-30) : Now;
        await using var context = database.CreateContext();
        var service = new AnonymousSessionService(context,
            new AnonymousSessionPolicy(new FixedTimeProvider(createdAt)));
        var saved = await service.CreateAsync(token);
        var before = await context.AnonymousSessions.CountAsync();

        using var factory = new SessionApiFactory(database);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = false,
            AllowAutoRedirect = false
        });
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/sessions/me");
        var cookie = scenario switch
        {
            "missing" => null,
            "malformed" => "short",
            "unknown" => SessionToken.Generate(),
            _ => token
        };
        if (cookie is not null)
            request.Headers.Add("Cookie", $"__Host-pathway_session={cookie}");

        using var response = await client.SendAsync(request);
        Assert.Equal(expected, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.False(response.Headers.Contains("Set-Cookie"));
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(token, body);
        Assert.DoesNotContain(saved.TokenHash, body);
        Assert.DoesNotContain(saved.Id.ToString(), body);
        if (expected == HttpStatusCode.OK) Assert.Empty(body);

        await using var verify = database.CreateContext();
        Assert.Equal(before, await verify.AnonymousSessions.CountAsync());
        var after = await verify.AnonymousSessions.SingleAsync(x => x.Id == saved.Id);
        Assert.Equal(saved.LastSeenAt, after.LastSeenAt);
        Assert.Equal(saved.ExpiresAt, after.ExpiresAt);
    }

    private sealed class SessionApiFactory(PostgresFixture database) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Production");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<PathwayDbContext>();
                services.AddScoped(_ => database.CreateContext());
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(new FixedTimeProvider(Now));
            });
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
