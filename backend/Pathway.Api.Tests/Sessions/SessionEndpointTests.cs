using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
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

    [Theory]
    [InlineData("missing")]
    [InlineData("malformed")]
    [InlineData("unknown")]
    [InlineData("expired")]
    public async Task Anonymous_replaces_invalid_or_missing_sessions(string scenario)
    {
        using var factory = new SessionApiFactory(database);
        using var client = CreateClient(factory);
        var (csrfCookie, csrfToken) = await GetCsrf(client);
        var oldToken = SessionToken.Generate();
        await using var context = database.CreateContext();
        if (scenario == "expired")
            await new AnonymousSessionService(context,
                new AnonymousSessionPolicy(new FixedTimeProvider(Now.AddDays(-30)))).CreateAsync(oldToken);
        var before = await context.AnonymousSessions.CountAsync();
        var supplied = scenario == "malformed" ? "short" : oldToken;
        var cookies = csrfCookie + (scenario == "missing" ? "" : $"; __Host-pathway_session={supplied}");
        using var response = await Post(client, cookies, csrfToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.Empty(await response.Content.ReadAsStringAsync());
        var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"));
        Assert.Contains("__Host-pathway_session=", cookie);
        Assert.Contains("secure", cookie.ToLowerInvariant());
        Assert.Contains("httponly", cookie.ToLowerInvariant());
        Assert.Contains("samesite=lax", cookie.ToLowerInvariant());
        Assert.Contains("path=/", cookie.ToLowerInvariant());
        Assert.DoesNotContain("domain=", cookie.ToLowerInvariant());
        Assert.DoesNotContain("max-age=", cookie.ToLowerInvariant());
        var token = cookie.Split(';')[0].Split('=', 2)[1];
        Assert.True(SessionToken.IsWellFormed(token));
        Assert.NotEqual(oldToken, token);
        await using var verify = database.CreateContext();
        Assert.Equal(before + 1, await verify.AnonymousSessions.CountAsync());
        var saved = await verify.AnonymousSessions.SingleAsync(x => x.TokenHash == SessionToken.Hash(token));
        Assert.Equal(Now.UtcDateTime, saved.CreatedAt);
        Assert.Equal(Now.AddDays(30).UtcDateTime, saved.ExpiresAt);
        var parsed = Microsoft.Net.Http.Headers.SetCookieHeaderValue.Parse(cookie);
        Assert.Equal(Now.AddDays(30), parsed.Expires);
    }

    [Fact]
    public async Task Reuse_does_not_write_extend_expiry_or_reissue_cookie_and_all_posts_are_limited()
    {
        using var factory = new SessionApiFactory(database);
        using var client = CreateClient(factory);
        var (csrfCookie, csrfToken) = await GetCsrf(client);
        var token = SessionToken.Generate();
        await using var context = database.CreateContext();
        var saved = await new AnonymousSessionService(context,
            new AnonymousSessionPolicy(new FixedTimeProvider(Now.AddDays(-1)))).CreateAsync(token);
        var before = await context.AnonymousSessions.CountAsync();
        var cookies = $"{csrfCookie}; __Host-pathway_session={token}";
        for (var i = 0; i < 20; i++)
        {
            using var response = await Post(client, cookies, csrfToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.False(response.Headers.Contains("Set-Cookie"));
            Assert.Empty(await response.Content.ReadAsStringAsync());
        }
        using var rejected = await Post(client, cookies, csrfToken);
        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.False(rejected.Headers.Contains("Set-Cookie"));
        using var me = new HttpRequestMessage(HttpMethod.Get, "/api/v1/sessions/me");
        me.Headers.Add("Cookie", cookies);
        using var checkedSession = await client.SendAsync(me);
        Assert.Equal(HttpStatusCode.OK, checkedSession.StatusCode);
        await using var verify = database.CreateContext();
        Assert.Equal(before, await verify.AnonymousSessions.CountAsync());
        var after = await verify.AnonymousSessions.SingleAsync(x => x.Id == saved.Id);
        Assert.Equal(saved.ExpiresAt, after.ExpiresAt);
        Assert.Equal(saved.LastSeenAt, after.LastSeenAt);
    }

    [Fact]
    public async Task Creation_is_limited_to_twenty_posts_without_database_writes_on_rejection()
    {
        using var factory = new SessionApiFactory(database);
        using var client = CreateClient(factory);
        var (cookie, token) = await GetCsrf(client);
        await using var context = database.CreateContext();
        var before = await context.AnonymousSessions.CountAsync();
        for (var i = 0; i < 20; i++)
        {
            using var response = await Post(client, cookie, token);
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }
        using var rejected = await Post(client, cookie, token);
        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.False(rejected.Headers.Contains("Set-Cookie"));
        await using var verify = database.CreateContext();
        Assert.Equal(before + 20, await verify.AnonymousSessions.CountAsync());
    }

    [Theory]
    [InlineData("missing-header")]
    [InlineData("wrong-header")]
    [InlineData("missing-cookie")]
    [InlineData("mismatched-pair")]
    public async Task Csrf_failure_rejects_post_without_writes(string scenario)
    {
        using var factory = new SessionApiFactory(database);
        using var client = CreateClient(factory);
        var (cookie, token) = await GetCsrf(client);
        var (_, otherToken) = await GetCsrf(client);
        await using var context = database.CreateContext();
        var before = await context.AnonymousSessions.CountAsync();
        using var response = await Post(client,
            scenario == "missing-cookie" ? null : cookie,
            scenario switch
            {
                "missing-header" => null,
                "wrong-header" => "invalid",
                "mismatched-pair" => otherToken,
                _ => token
            });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.False(response.Headers.Contains("Set-Cookie"));
        await using var verify = database.CreateContext();
        Assert.Equal(before, await verify.AnonymousSessions.CountAsync());
    }

    private static HttpClient CreateClient(SessionApiFactory factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = false,
            AllowAutoRedirect = false
        });

    private static async Task<(string Cookie, string Token)> GetCsrf(HttpClient client)
    {
        using var response = await client.GetAsync("/api/v1/antiforgery");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
        var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"));
        Assert.StartsWith("__Host-pathway_antiforgery=", cookie);
        Assert.Contains("secure", cookie.ToLowerInvariant());
        Assert.Contains("httponly", cookie.ToLowerInvariant());
        Assert.Contains("samesite=strict", cookie.ToLowerInvariant());
        Assert.Contains("path=/", cookie.ToLowerInvariant());
        Assert.DoesNotContain("domain=", cookie.ToLowerInvariant());
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return (cookie.Split(';')[0], body.GetProperty("requestToken").GetString()!);
    }

    private static Task<HttpResponseMessage> Post(HttpClient client, string? cookie, string? token)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/sessions/anonymous");
        if (cookie is not null) request.Headers.Add("Cookie", cookie);
        if (token is not null) request.Headers.Add("X-CSRF-TOKEN", token);
        return Send();
        async Task<HttpResponseMessage> Send()
        {
            using (request) return await client.SendAsync(request);
        }
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
