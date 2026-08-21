using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using MarketPulse.Application.Services.SearchQueryGenerator;
using MarketPulse.Infrastructure.AI;

namespace MarketPulse.UnitTests.Infrastructure;

public class OpenRouterAiSearchQueryClientTests
{
    [Fact]
    public async Task RequestPayload_UsesStrictApplicationSchemaAndOnlyPromptMessages()
    {
        string? requestBody = null;
        AuthenticationHeaderValue? authorization = null;
        var handler = new StubHttpMessageHandler(async (request, cancellationToken) =>
        {
            requestBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            authorization = request.Headers.Authorization;
            return JsonResponse(
                JsonSerializer.Serialize(new
                {
                    choices = new[]
                    {
                        new
                        {
                            message = new { content = "{\"queries\":[]}" }
                        }
                    }
                }));
        });
        var options = CreateOptions();
        var client = new OpenRouterAiSearchQueryClient(
            new HttpClient(handler),
            options);
        var prompt = CreatePrompt();

        await client.GenerateAsync(prompt);

        Assert.NotNull(requestBody);
        using var document = JsonDocument.Parse(requestBody);
        var root = document.RootElement;
        Assert.Equal(options.Model, root.GetProperty("model").GetString());
        Assert.False(root.GetProperty("stream").GetBoolean());
        Assert.Equal("Bearer", authorization?.Scheme);
        Assert.Equal(options.ApiKey, authorization?.Parameter);

        var messages = root.GetProperty("messages");
        Assert.Equal(2, messages.GetArrayLength());
        Assert.Equal(prompt.SystemPrompt, messages[0].GetProperty("content").GetString());
        Assert.Equal(prompt.UserPrompt, messages[1].GetProperty("content").GetString());
        Assert.True(
            root.GetProperty("provider")
                .GetProperty("require_parameters")
                .GetBoolean());

        var responseFormat = root.GetProperty("response_format");
        Assert.Equal("json_schema", responseFormat.GetProperty("type").GetString());
        var jsonSchema = responseFormat.GetProperty("json_schema");
        Assert.Equal(prompt.ResponseSchemaName, jsonSchema.GetProperty("name").GetString());
        Assert.True(jsonSchema.GetProperty("strict").GetBoolean());
        Assert.True(
            JsonNode.DeepEquals(
                JsonNode.Parse(prompt.ResponseJsonSchema),
                JsonNode.Parse(jsonSchema.GetProperty("schema").GetRawText())));
    }

    private static AiSearchQueryPrompt CreatePrompt()
        => new(
            "system prompt",
            "user prompt",
            "search_queries",
            """
            {
              "type": "object",
              "additionalProperties": false,
              "required": ["queries"],
              "properties": {
                "queries": { "type": "array" }
              }
            }
            """);

    private static OpenRouterOptions CreateOptions()
        => new()
        {
            ApiKey = "test-key",
            Model = "test/model",
            Endpoint = "https://openrouter.test/api/v1/chat/completions",
            Temperature = 0.2,
            MaxTokens = 800
        };

    private static HttpResponseMessage JsonResponse(string body)
        => new(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
            => handler(request, cancellationToken);
    }
}
