using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Configuration;
using Pathway.Api.Application.Planning;
using Pathway.Api.Infrastructure.Planning;

namespace Pathway.Api.Tests.Planning;

public sealed class PlanGeneratorTests
{
    private static IConfiguration Config(bool configured = true) => new ConfigurationBuilder()
        .AddInMemoryCollection(configured ? new Dictionary<string, string?>
        {
            ["Planning:Endpoint"] = "http://localhost:11434/api/generate", ["Planning:Model"] = "test-model"
        } : []).Build();

    [Fact]
    public async Task Sends_structured_schema_and_parses_valid_output_without_dates()
    {
        string? body = null;
        using var http = new HttpClient(new Handler(async request =>
        {
            body = await request.Content!.ReadAsStringAsync();
            return Envelope(ValidPlan());
        }));
        var result = await new OllamaPlanGenerator(http, Config()).GenerateAsync(
            new("My private goal", "Description", new(null, null, null)), default);
        Assert.Equal(2, result.Milestones[0].Tasks[0].EstimatedHours);
        using var requestBody = JsonDocument.Parse(body!);
        Assert.False(requestBody.RootElement.GetProperty("stream").GetBoolean());
        Assert.Equal("object", requestBody.RootElement.GetProperty("format").GetProperty("type").GetString());
        Assert.Contains("My private goal", requestBody.RootElement.GetProperty("prompt").GetString());
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("not json")]
    [InlineData("{\"milestones\":[]}")]
    [InlineData("{\"milestones\":[{\"title\":\"M\",\"tasks\":[{\"title\":\"T\",\"estimatedHours\":0}]}]}")]
    [InlineData("{\"milestones\":[{\"title\":\"M\",\"tasks\":[{\"title\":\"T\",\"estimatedHours\":1.5}]}]}")]
    public async Task Malformed_or_invalid_model_output_is_rejected(string content)
    {
        using var http = new HttpClient(new Handler(_ => Task.FromResult(Envelope(content))));
        await Assert.ThrowsAsync<PlanningOutputException>(() =>
            new OllamaPlanGenerator(http, Config()).GenerateAsync(new("Goal", null, new(null, null, null)), default));
    }

    [Theory]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("\"private provider message\"")]
    [InlineData("42")]
    [InlineData("true")]
    public async Task Non_object_envelope_is_a_safe_output_error(string envelope)
    {
        using var http = new HttpClient(new Handler(_ => Task.FromResult(RawEnvelope(envelope))));
        var error = await Assert.ThrowsAsync<PlanningOutputException>(() =>
            new OllamaPlanGenerator(http, Config()).GenerateAsync(new("Goal", null, new(null, null, null)), default));
        Assert.DoesNotContain("private", error.Message);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"done\":false,\"done_reason\":\"stop\"}")]
    [InlineData("{\"done\":null,\"done_reason\":\"stop\"}")]
    [InlineData("{\"done\":\"true\",\"done_reason\":\"stop\"}")]
    [InlineData("{\"done\":1,\"done_reason\":\"stop\"}")]
    [InlineData("{\"done_reason\":\"stop\"}")]
    [InlineData("{\"done\":true}")]
    [InlineData("{\"done\":true,\"done_reason\":null}")]
    [InlineData("{\"done\":true,\"done_reason\":false}")]
    [InlineData("{\"done\":true,\"done_reason\":42}")]
    [InlineData("{\"done\":true,\"done_reason\":{}}")]
    [InlineData("{\"done\":true,\"done_reason\":\"\"}")]
    [InlineData("{\"done\":true,\"done_reason\":\"length\"}")]
    [InlineData("{\"done\":true,\"done_reason\":\"load\"}")]
    [InlineData("{\"done\":true,\"done_reason\":\"unload\"}")]
    [InlineData("{\"done\":true,\"done_reason\":\"unknown\"}")]
    public async Task Unconfirmed_or_truncated_generation_is_rejected_even_with_valid_plan(string completion)
    {
        var envelope = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(completion)!;
        envelope["response"] = JsonSerializer.SerializeToElement(ValidPlan());
        using var http = new HttpClient(new Handler(_ => Task.FromResult(RawEnvelope(JsonSerializer.Serialize(envelope)))));
        await Assert.ThrowsAsync<PlanningOutputException>(() =>
            new OllamaPlanGenerator(http, Config()).GenerateAsync(new("Goal", null, new(null, null, null)), default));
    }

    [Theory]
    [InlineData("plan")]
    [InlineData("milestone")]
    [InlineData("task")]
    [InlineData("resource")]
    public async Task Unknown_fields_at_every_plan_level_are_rejected(string level)
    {
        var plan = JsonNode.Parse(ValidPlan())!;
        var milestone = plan["milestones"]![0]!;
        var target = level switch { "task" => milestone["tasks"]![0]!,
            "resource" => milestone["resources"]![0]!, "milestone" => milestone, _ => plan };
        target["privateExtra"] = "private provider message";
        using var http = new HttpClient(new Handler(_ => Task.FromResult(Envelope(plan.ToJsonString()))));
        var error = await Assert.ThrowsAsync<PlanningOutputException>(() =>
            new OllamaPlanGenerator(http, Config()).GenerateAsync(new("Goal", null, new(null, null, null)), default));
        Assert.DoesNotContain("private", error.Message);
    }

    [Theory]
    [InlineData("milestones", "Milestones")]
    [InlineData("title", "Title")]
    [InlineData("estimatedHours", "EstimatedHours")]
    public async Task Plan_property_names_must_match_the_schema_case(string original, string replacement)
    {
        using var http = new HttpClient(new Handler(_ => Task.FromResult(
            Envelope(ValidPlan().Replace('"' + original + '"', '"' + replacement + '"')))));
        await Assert.ThrowsAsync<PlanningOutputException>(() =>
            new OllamaPlanGenerator(http, Config()).GenerateAsync(new("Goal", null, new(null, null, null)), default));
    }

    [Fact]
    public async Task Normal_completion_accepts_provider_metadata_and_preserves_the_plan()
    {
        using var http = new HttpClient(new Handler(_ => Task.FromResult(RawEnvelope(JsonSerializer.Serialize(new
        {
            response = ValidPlan(), done = true, done_reason = "stop", model = "test-model",
            total_duration = 1234, futureMetadata = new { value = true }
        })))));
        var plan = await new OllamaPlanGenerator(http, Config()).GenerateAsync(new("Goal", null, new(null, null, null)), default);
        Assert.Equal("Milestone", Assert.Single(plan.Milestones).Title);
        var task = Assert.Single(plan.Milestones[0].Tasks);
        Assert.Equal("Task", task.Title);
        Assert.Equal(2, task.EstimatedHours);
        Assert.NotEmpty(task.KeyPoints!);
        Assert.NotEmpty(plan.Milestones[0].Resources!);
    }

    [Fact]
    public async Task Generation_without_guidance_is_a_safe_output_error()
    {
        using var http = new HttpClient(new Handler(_ => Task.FromResult(Envelope(
            JsonSerializer.Serialize(SchedulerTests.Plan(2), new JsonSerializerOptions(JsonSerializerDefaults.Web))))));
        await Assert.ThrowsAsync<PlanningOutputException>(() =>
            new OllamaPlanGenerator(http, Config()).GenerateAsync(new("Goal", null, new(null, null, null)), default));
    }

    [Fact]
    public async Task Oversized_response_is_rejected()
    {
        using var http = new HttpClient(new Handler(_ => Task.FromResult(Envelope(new string('x', 140000)))));
        await Assert.ThrowsAsync<PlanningOutputException>(() =>
            new OllamaPlanGenerator(http, Config()).GenerateAsync(new("Goal", null, new(null, null, null)), default));
    }

    [Theory]
    [InlineData("status")]
    [InlineData("network")]
    [InlineData("timeout")]
    public async Task Upstream_failures_are_retryable_without_disclosing_provider_content(string failure)
    {
        using var http = new HttpClient(new Handler(_ => failure switch
        {
            "network" => throw new HttpRequestException("private provider message"),
            "timeout" => throw new TaskCanceledException("private provider message"),
            _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) { Content = new StringContent("private") })
        }));
        var error = await Assert.ThrowsAsync<PlanningUnavailableException>(() =>
            new OllamaPlanGenerator(http, Config()).GenerateAsync(new("Goal", null, new(null, null, null)), default));
        Assert.DoesNotContain("private", error.Message);
    }

    [Fact]
    public async Task Unconfigured_provider_never_sends_requests()
    {
        using var http = new HttpClient(new Handler(_ => throw new Exception("Must not call")));
        await Assert.ThrowsAsync<PlanningUnavailableException>(() =>
            new OllamaPlanGenerator(http, Config(false)).GenerateAsync(new("Goal", null, new(null, null, null)), default));
    }

    private static string ValidPlan() => JsonSerializer.Serialize(GuidanceValidationTests.GuidedPlan(),
        new JsonSerializerOptions(JsonSerializerDefaults.Web));

    private static HttpResponseMessage Envelope(string content) => RawEnvelope(
        JsonSerializer.Serialize(new { response = content, done = true, done_reason = "stop" }));

    private static HttpResponseMessage RawEnvelope(string content) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(content, Encoding.UTF8, "application/json")
    };
    private sealed class Handler(Func<HttpRequestMessage, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => send(request);
    }
}
