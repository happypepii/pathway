using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Pathway.Api.Tests.Sessions;

// Security rejection must happen without opening a database connection.
public sealed class SessionSecurityTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task Invalid_csrf_is_rejected_before_database_access(bool cookie, bool header)
    {
        using var factory = new SecurityFactory();
        using var client = Client(factory);
        using var csrf = await client.GetAsync("/api/v1/antiforgery");
        Assert.Equal(HttpStatusCode.OK, csrf.StatusCode);
        Assert.True(csrf.Headers.CacheControl?.NoStore);
        var setCookie = Assert.Single(csrf.Headers.GetValues("Set-Cookie"));
        Assert.StartsWith("__Host-pathway_antiforgery=", setCookie);
        Assert.Contains("secure", setCookie.ToLowerInvariant());
        Assert.Contains("httponly", setCookie.ToLowerInvariant());
        Assert.Contains("samesite=strict", setCookie.ToLowerInvariant());
        var body = await csrf.Content.ReadFromJsonAsync<JsonElement>();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/sessions/anonymous");
        if (cookie) request.Headers.Add("Cookie", setCookie.Split(';')[0]);
        if (header) request.Headers.Add("X-CSRF-TOKEN", body.GetProperty("requestToken").GetString());
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.False(response.Headers.Contains("Set-Cookie"));
    }

    [Fact]
    public async Task Rate_limit_counts_rejected_posts_but_does_not_limit_get()
    {
        using var factory = new SecurityFactory();
        using var client = Client(factory);
        for (var i = 0; i < 20; i++)
        {
            using var response = await client.PostAsync("/api/v1/sessions/anonymous", null);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
        using var blocked = await client.PostAsync("/api/v1/sessions/anonymous", null);
        Assert.Equal(HttpStatusCode.TooManyRequests, blocked.StatusCode);
        using var get = await client.GetAsync("/api/v1/antiforgery");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
    }

    [Fact]
    public async Task Invalid_token_pair_is_rejected()
    {
        using var factory = new SecurityFactory();
        using var client = Client(factory);
        using var first = await client.GetAsync("/api/v1/antiforgery");
        using var second = await client.GetAsync("/api/v1/antiforgery");
        var secondBody = await second.Content.ReadFromJsonAsync<JsonElement>();
        foreach (var header in new[] { "invalid", secondBody.GetProperty("requestToken").GetString()! })
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/sessions/anonymous");
            request.Headers.Add("Cookie", first.Headers.GetValues("Set-Cookie").Single().Split(';')[0]);
            request.Headers.Add("X-CSRF-TOKEN", header);
            using var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
    }

    [Fact]
    public async Task Limiter_partitions_by_connection_ip_and_ignores_untrusted_forwarded_headers()
    {
        using var factory = new SecurityFactory();
        using var client = Client(factory);
        client.DefaultRequestHeaders.Add("X-Test-Connection-IP", "192.0.2.1");
        for (var i = 0; i < 20; i++)
        {
            using var response = await client.PostAsync("/api/v1/sessions/anonymous", null);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "192.0.2.2");
        using var blocked = await client.PostAsync("/api/v1/sessions/anonymous", null);
        Assert.Equal(HttpStatusCode.TooManyRequests, blocked.StatusCode);
        client.DefaultRequestHeaders.Remove("X-Test-Connection-IP");
        client.DefaultRequestHeaders.Add("X-Test-Connection-IP", "::ffff:192.0.2.1");
        using var mapped = await client.PostAsync("/api/v1/sessions/anonymous", null);
        Assert.Equal(HttpStatusCode.TooManyRequests, mapped.StatusCode);
        client.DefaultRequestHeaders.Remove("X-Test-Connection-IP");
        client.DefaultRequestHeaders.Add("X-Test-Connection-IP", "192.0.2.3");
        using var other = await client.PostAsync("/api/v1/sessions/anonymous", null);
        Assert.Equal(HttpStatusCode.BadRequest, other.StatusCode);
    }

    // Only the test host accepts this synthetic connection-IP header.
    private sealed class TestConnectionIp : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use(async (context, proceed) =>
            {
                if (IPAddress.TryParse(context.Request.Headers["X-Test-Connection-IP"], out var address))
                    context.Connection.RemoteIpAddress = address;
                await proceed();
            });
            next(app);
        };
    }

    private static HttpClient Client(SecurityFactory factory) => factory.CreateClient(
        new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = false,
            AllowAutoRedirect = false
        });

    private sealed class SecurityFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Production");
            builder.ConfigureServices(services => services.AddTransient<IStartupFilter, TestConnectionIp>());
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:Pathway"] =
                        "Host=127.0.0.1;Port=1;Database=unused;Username=unused;Password=unused;Timeout=1"
                }));
        }
    }
}
