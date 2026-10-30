using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Pathway.Api.Application.Planning;
using Pathway.Api.Tests.Goals;
using Pathway.Api.Tests.Persistence;

namespace Pathway.Api.Tests.Planning;

public sealed class PlanningEndpointTests(PostgresFixture database) : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    public async Task InitializeAsync()
    {
        await using var context = database.CreateContext();
        await context.Goals.ExecuteDeleteAsync();
        await context.AnonymousSessions.ExecuteDeleteAsync();
    }
    public Task DisposeAsync() => Task.CompletedTask;

    private sealed class Generator : IPlanGenerator
    {
        public bool Invalid { get; set; }
        public bool Unavailable { get; set; }
        public PlanningContext? Context { get; private set; }
        public Task<PlanInput> GenerateAsync(PlanningContext context, CancellationToken ct)
        {
            Context = context;
            if (Unavailable) throw new PlanningUnavailableException();
            return Task.FromResult(Invalid ? SchedulerTests.Plan(0) : SchedulerTests.Plan(1, 2));
        }
    }

    private sealed class Factory(PostgresFixture database, Generator generator) : GoalEndpointTests.GoalApiFactory(database)
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IPlanGenerator>();
                services.AddSingleton<IPlanGenerator>(generator);
            });
        }
    }

    private static async Task<string> Create(HttpClient client)
    {
        using var response = await client.PostAsJsonAsync("/api/v1/goals", GoalEndpointTests.Input());
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return response.Headers.Location!.ToString();
    }

    private static async Task<JsonElement> Preview(HttpClient client, string url)
    {
        using var response = await client.PostAsync(url + "/plan-preview", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    [Fact]
    public async Task Complete_journey_previews_without_persistence_confirms_and_tracks_progress()
    {
        var generator = new Generator();
        using var factory = new Factory(database, generator);
        using var client = await GoalEndpointTests.Bootstrap(factory);
        var url = await Create(client);
        using var answers = await client.PutAsJsonAsync(url + "/planning-answers",
            new AnswerInput("Beginner", null, "No garden, balcony only"));
        Assert.Equal(HttpStatusCode.NoContent, answers.StatusCode);
        var preview = await Preview(client, url);
        Assert.Equal("Beginner", generator.Context!.Answers.Experience);
        await using (var context = database.CreateContext())
        {
            Assert.Equal(0, await context.Tasks.CountAsync());
            Assert.Equal(0, await context.Milestones.CountAsync());
        }
        using var confirm = await client.PostAsJsonAsync(url + "/plan", new
        {
            milestones = preview.GetProperty("milestones"),
            goalVersion = preview.GetProperty("goalVersion")
        });
        Assert.Equal(HttpStatusCode.OK, confirm.StatusCode);
        var saved = await client.GetFromJsonAsync<JsonElement>(url);
        Assert.Equal("Active", saved.GetProperty("status").GetString());
        var tasks = saved.GetProperty("milestones")[0].GetProperty("tasks");
        Assert.Equal(2, tasks.GetArrayLength());
        var ids = tasks.EnumerateArray().Select(x => x.GetProperty("id").GetString()!).ToArray();
        foreach (var id in ids)
            Assert.Equal(HttpStatusCode.NoContent,
                (await client.PatchAsJsonAsync($"/api/v1/tasks/{id}/completion", new { completed = true })).StatusCode);
        saved = await client.GetFromJsonAsync<JsonElement>(url);
        Assert.Equal(100, saved.GetProperty("progress").GetInt32());
        Assert.Equal("Completed", saved.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, saved.GetProperty("nextTask").ValueKind);
        Assert.Equal(HttpStatusCode.NoContent,
            (await client.PatchAsJsonAsync($"/api/v1/tasks/{ids[0]}/completion", new { completed = false })).StatusCode);
        saved = await client.GetFromJsonAsync<JsonElement>(url);
        Assert.Equal("Active", saved.GetProperty("status").GetString());
        Assert.Equal(50, saved.GetProperty("progress").GetInt32());
        Assert.Equal(ids[0], saved.GetProperty("nextTask").GetProperty("id").GetString());
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync(url + "/plan-preview", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,
            (await client.PostAsJsonAsync(url + "/plan", new { milestones = preview.GetProperty("milestones"), goalVersion = preview.GetProperty("goalVersion") })).StatusCode);
        await using (var context = database.CreateContext())
        {
            var effort = await context.Tasks.Select(x => x.EstimatedMinutes).ToListAsync();
            Assert.Equal(new[] { 60, 120 }, effort.Order().ToArray());
        }
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync(url)).StatusCode);
        await using var verify = database.CreateContext();
        Assert.Empty(await verify.Tasks.ToListAsync());
        Assert.Empty(await verify.Milestones.ToListAsync());
        Assert.Empty(await verify.PlanningAnswers.ToListAsync());
    }

    [Fact]
    public async Task All_planning_and_task_operations_check_ownership()
    {
        using var factory = new Factory(database, new());
        using var owner = await GoalEndpointTests.Bootstrap(factory);
        using var other = await GoalEndpointTests.Bootstrap(factory);
        var url = await Create(owner);
        var preview = await Preview(owner, url);
        var confirmation = new { milestones = preview.GetProperty("milestones"), goalVersion = preview.GetProperty("goalVersion") };
        Assert.Equal(HttpStatusCode.NotFound, (await other.PutAsJsonAsync(url + "/planning-answers", new AnswerInput(null, null, null))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PostAsync(url + "/plan-preview", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PostAsJsonAsync(url + "/plan", confirmation)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await owner.PostAsJsonAsync(url + "/plan", confirmation)).StatusCode);
        var saved = await owner.GetFromJsonAsync<JsonElement>(url);
        var id = saved.GetProperty("milestones")[0].GetProperty("tasks")[0].GetProperty("id").GetString();
        Assert.Equal(HttpStatusCode.NotFound, (await other.PatchAsJsonAsync($"/api/v1/tasks/{id}/completion", new { completed = true })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PatchAsJsonAsync($"/api/v1/tasks/{id}", new { title = "stolen" })).StatusCode);
        saved = await owner.GetFromJsonAsync<JsonElement>(url);
        Assert.Equal(0, saved.GetProperty("progress").GetInt32());
    }

    [Fact]
    public async Task Invalid_provider_output_or_unavailability_leaves_database_untouched()
    {
        var generator = new Generator { Invalid = true };
        using var factory = new Factory(database, generator);
        using var client = await GoalEndpointTests.Bootstrap(factory);
        var url = await Create(client);
        Assert.Equal(HttpStatusCode.BadGateway, (await client.PostAsync(url + "/plan-preview", null)).StatusCode);
        generator.Invalid = false;
        generator.Unavailable = true;
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.PostAsync(url + "/plan-preview", null)).StatusCode);
        var saved = await client.GetFromJsonAsync<JsonElement>(url);
        Assert.Equal("Draft", saved.GetProperty("status").GetString());
        Assert.Equal(0, saved.GetProperty("milestones").GetArrayLength());
    }

    [Fact]
    public async Task Stale_or_invalid_confirmation_does_not_save_a_partial_plan()
    {
        using var factory = new Factory(database, new());
        using var client = await GoalEndpointTests.Bootstrap(factory);
        var url = await Create(client);
        var preview = await Preview(client, url);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsJsonAsync(url + "/planning-answers", new AnswerInput("Changed", null, null))).StatusCode);
        var confirmation = new { milestones = preview.GetProperty("milestones"), goalVersion = preview.GetProperty("goalVersion") };
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(url + "/plan", confirmation)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(url + "/plan", new { milestones = Array.Empty<object>(), goalVersion = Guid.NewGuid() })).StatusCode);
        var saved = await client.GetFromJsonAsync<JsonElement>(url);
        Assert.Equal("Draft", saved.GetProperty("status").GetString());
        Assert.Equal(0, saved.GetProperty("milestones").GetArrayLength());
    }

    [Fact]
    public async Task Confirmation_recalculates_untrusted_dates_and_preserves_infeasible_warning()
    {
        using var factory = new Factory(database, new());
        using var client = await GoalEndpointTests.Bootstrap(factory);
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow,
            TimeZoneInfo.FindSystemTimeZoneById("Asia/Taipei")).DateTime);
        using var created = await client.PostAsJsonAsync("/api/v1/goals",
            GoalEndpointTests.Input() with { TargetDate = today, AvailableHoursPerWeek = 1 });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var url = created.Headers.Location!.ToString();
        var preview = await Preview(client, url);
        using var confirmed = await client.PostAsJsonAsync(url + "/plan", new
        {
            goalVersion = preview.GetProperty("goalVersion"),
            milestones = new[] { new { title = "Work", tasks = new[] {
                new { title = "Task", estimatedHours = 2, scheduledStartDate = "1900-01-01", scheduledEndDate = "1900-01-01" } } } }
        });
        Assert.Equal(HttpStatusCode.OK, confirmed.StatusCode);
        var result = await confirmed.Content.ReadFromJsonAsync<JsonElement>();
        var task = result.GetProperty("milestones")[0].GetProperty("tasks")[0];
        Assert.Equal(today.ToString("yyyy-MM-dd"), task.GetProperty("scheduledStartDate").GetString());
        Assert.Equal(today.AddDays(13).ToString("yyyy-MM-dd"), task.GetProperty("scheduledEndDate").GetString());
        Assert.False(result.GetProperty("schedule").GetProperty("isFeasible").GetBoolean());
        Assert.Equal(2, result.GetProperty("schedule").GetProperty("totalEstimatedHours").GetInt32());
        using var metadata = await client.PatchAsJsonAsync(url, GoalEndpointTests.Input() with
            { Title = "Updated active title", TargetDate = today, AvailableHoursPerWeek = 1 });
        Assert.Equal(HttpStatusCode.OK, metadata.StatusCode);
        using var constraints = await client.PatchAsJsonAsync(url, GoalEndpointTests.Input() with
            { Title = "Updated active title", TargetDate = today, AvailableHoursPerWeek = 2 });
        Assert.Equal(HttpStatusCode.Conflict, constraints.StatusCode);
        var taskId = task.GetProperty("id").GetString();
        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.PatchAsJsonAsync($"/api/v1/tasks/{taskId}/completion", new { })).StatusCode);
    }

    [Fact]
    public async Task Commit_failure_rolls_back_goal_milestones_and_tasks()
    {
        using var factory = new Factory(database, new());
        using var client = await GoalEndpointTests.Bootstrap(factory);
        var url = await Create(client);
        var saved = await client.GetFromJsonAsync<JsonElement>(url);
        var id = saved.GetProperty("id").GetGuid();
        Guid owner;
        await using (var reader = database.CreateContext())
            owner = (await reader.Goals.SingleAsync(g => g.Id == id)).SessionId;
        await using (var failing = database.CreateContext(new FailCommit()))
        {
            var service = new PlanningService(failing, TimeProvider.System, new Generator());
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.Confirm(owner, id,
                new ConfirmationInput(SchedulerTests.Plan(1, 2).Milestones, saved.GetProperty("goalVersion").GetGuid()), default));
        }
        await using var verify = database.CreateContext();
        Assert.Equal(Pathway.Api.Domain.GoalStatus.Draft, (await verify.Goals.SingleAsync(g => g.Id == id)).Status);
        Assert.Empty(await verify.Milestones.ToListAsync());
        Assert.Empty(await verify.Tasks.ToListAsync());
    }

    private sealed class FailCommit : Microsoft.EntityFrameworkCore.Diagnostics.DbTransactionInterceptor
    {
        public override ValueTask<Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult> TransactionCommittingAsync(
            System.Data.Common.DbTransaction transaction,
            Microsoft.EntityFrameworkCore.Diagnostics.TransactionEventData eventData,
            Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult result,
            CancellationToken cancellationToken = default) => throw new InvalidOperationException("Injected commit failure");
    }

    [Fact]
    public async Task Concurrent_confirmations_create_exactly_one_plan()
    {
        using var factory = new Factory(database, new());
        using var client = await GoalEndpointTests.Bootstrap(factory);
        var url = await Create(client);
        var preview = await Preview(client, url);
        var confirmation = new { milestones = preview.GetProperty("milestones"), goalVersion = preview.GetProperty("goalVersion") };
        var responses = await Task.WhenAll(client.PostAsJsonAsync(url + "/plan", confirmation), client.PostAsJsonAsync(url + "/plan", confirmation));
        Assert.Equal(new[] { HttpStatusCode.OK, HttpStatusCode.Conflict }, responses.Select(x => x.StatusCode).Order().ToArray());
        foreach (var response in responses) response.Dispose();
        var saved = await client.GetFromJsonAsync<JsonElement>(url);
        Assert.Equal(1, saved.GetProperty("milestones").GetArrayLength());
        Assert.Equal(2, saved.GetProperty("milestones")[0].GetProperty("tasks").GetArrayLength());
    }
}
