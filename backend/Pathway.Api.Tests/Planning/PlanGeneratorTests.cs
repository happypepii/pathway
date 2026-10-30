using System.Net;
using System.Text;
using System.Text.Json;
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
            return Envelope(JsonSerializer.Serialize(SchedulerTests.Plan(2), new JsonSerializerOptions(JsonSerializerDefaults.Web)));
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

    private static HttpResponseMessage Envelope(string content) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(JsonSerializer.Serialize(new { response = content }), Encoding.UTF8, "application/json")
    };
    private sealed class Handler(Func<HttpRequestMessage, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => send(request);
    }
}
