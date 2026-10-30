using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Pathway.Api.Infrastructure.Persistence;
using Pathway.Api.Tests.Persistence;

namespace Pathway.Api.Tests.Goals;

public sealed class GoalEndpointTests(PostgresFixture database) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task Goal_CRUD_is_persisted_and_isolated_by_session()
    {
        using var factory = new GoalApiFactory(database);
        using var owner = await Bootstrap(factory);
        using var other = await Bootstrap(factory);
        var input = Input();
        using var created = await owner.PostAsJsonAsync("/api/v1/goals", input);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.NotNull(created.Headers.Location);
        var goal = await created.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Learn gardening", goal.GetProperty("title").GetString());
        Assert.Equal("Draft", goal.GetProperty("status").GetString());
        var url = created.Headers.Location!.ToString();
        using var read = await owner.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PatchAsJsonAsync(url, input)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.DeleteAsync(url)).StatusCode);
        var otherList = await other.GetFromJsonAsync<JsonElement>("/api/v1/goals");
        Assert.Equal(0, otherList.GetArrayLength());
        using var updated = await owner.PatchAsJsonAsync(url, input with { Title = " Updated " });
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        var after = await owner.GetFromJsonAsync<JsonElement>(url);
        Assert.Equal("Updated", after.GetProperty("title").GetString());
        Assert.Equal(HttpStatusCode.NoContent, (await owner.DeleteAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync(url)).StatusCode);
    }

    [Fact]
    public async Task Invalid_inputs_return_validation_problem_and_missing_session_is_rejected()
    {
        using var factory = new GoalApiFactory(database);
        using var owner = await Bootstrap(factory);
        using var invalid = await owner.PostAsJsonAsync("/api/v1/goals", Input() with { Title = " ", AvailableHoursPerWeek = 169 });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        var body = await invalid.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("errors", out _));
        using var anonymous = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/v1/goals")).StatusCode);
    }

    internal static GoalInputForTest Input() => new("  Learn gardening  ", "A balcony garden",
        DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)), 7, "Asia/Taipei");

    internal sealed record GoalInputForTest(string Title, string? Description, DateOnly TargetDate,
        int AvailableHoursPerWeek, string TimeZoneId);

    internal static async Task<HttpClient> Bootstrap(GoalApiFactory factory)
    {
        var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var csrf = await client.GetFromJsonAsync<JsonElement>("/api/v1/antiforgery");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", csrf.GetProperty("requestToken").GetString());
        using var created = await client.PostAsync("/api/v1/sessions/anonymous", null);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        return client;
    }

    internal class GoalApiFactory(PostgresFixture database) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Production");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<PathwayDbContext>();
                services.AddScoped(_ => database.CreateContext());
            });
        }
    }
}
