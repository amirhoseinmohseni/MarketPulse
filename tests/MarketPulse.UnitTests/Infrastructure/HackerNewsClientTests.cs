using System.Net;
using System.Text.Json;
using MarketPulse.Application.Services.HackerNewsDataCollection;
using MarketPulse.Infrastructure.HackerNews;

namespace MarketPulse.UnitTests.Infrastructure;

public class HackerNewsClientTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SearchAsync_WhenQueryIsEmpty_ThrowsWithoutHttpCall(string query)
    {
        var handler = JsonHandler("{\"hits\":[]}");
        var client = CreateClient(handler);

        await Assert.ThrowsAsync<ArgumentException>(
            () => client.SearchAsync(new HackerNewsSearchRequest { Query = query }));
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task SearchAsync_BuildsEncodedUriWithConfiguredParameters()
    {
        Uri? capturedUri = null;
        var handler = new TestHttpMessageHandler((request, _) =>
        {
            capturedUri = request.RequestUri;
            return Task.FromResult(TestHttpMessageHandler.JsonResponse("{\"hits\":[]}"));
        });
        var client = CreateClient(
            handler,
            new HackerNewsOptions
            {
                BaseUrl = "https://hn.test/api/v1/",
                Tags = "story tag",
                DefaultLimit = 20
            });

        await client.SearchAsync(new HackerNewsSearchRequest
        {
            Query = "C# billing & tools",
            Limit = 7
        });

        Assert.Equal("https", capturedUri?.Scheme);
        Assert.Equal("hn.test", capturedUri?.Host);
        Assert.Equal("/api/v1/search", capturedUri?.AbsolutePath);
        var parameters = ParseQuery(capturedUri!);
        Assert.Equal("C# billing & tools", parameters["query"]);
        Assert.Equal("story tag", parameters["tags"]);
        Assert.Equal("7", parameters["hitsPerPage"]);
    }

    [Fact]
    public async Task SearchAsync_WhenRequestLimitIsInvalid_UsesConfiguredDefault()
    {
        Uri? capturedUri = null;
        var handler = new TestHttpMessageHandler((request, _) =>
        {
            capturedUri = request.RequestUri;
            return Task.FromResult(TestHttpMessageHandler.JsonResponse("{\"hits\":[]}"));
        });
        var client = CreateClient(handler, new HackerNewsOptions { DefaultLimit = 31 });

        await client.SearchAsync(new HackerNewsSearchRequest { Query = "idea", Limit = 0 });

        Assert.Equal("31", ParseQuery(capturedUri!)["hitsPerPage"]);
    }

    [Fact]
    public async Task SearchAsync_MapsPrimaryAndFallbackFieldsAndFiltersInvalidHits()
    {
        const long unixTime = 1_775_000_000;
        var createdAt = new DateTime(2026, 8, 20, 10, 0, 0, DateTimeKind.Utc);
        var body = JsonSerializer.Serialize(new
        {
            hits = new object[]
            {
                new
                {
                    objectID = "primary",
                    title = "Primary title",
                    story_title = "Ignored title",
                    story_text = "Primary content",
                    comment_text = "Ignored content",
                    url = "https://primary.test",
                    story_url = "https://ignored.test",
                    points = 10,
                    num_comments = 4,
                    created_at = createdAt,
                    created_at_i = unixTime
                },
                new
                {
                    objectID = "fallback",
                    title = "",
                    story_title = "Fallback title",
                    story_text = "",
                    comment_text = "Fallback content",
                    url = "",
                    story_url = "https://fallback.test",
                    created_at = (DateTime?)null,
                    created_at_i = unixTime
                },
                new { objectID = "", title = "Missing id" },
                new { objectID = "missing-title", title = "", story_title = "" }
            }
        });
        var client = CreateClient(JsonHandler(body));

        var results = await client.SearchAsync(new HackerNewsSearchRequest { Query = "idea" });

        Assert.Equal(2, results.Count);
        var primary = results[0];
        Assert.Equal("Primary title", primary.Title);
        Assert.Equal("Primary content", primary.Content);
        Assert.Equal("https://primary.test", primary.Url);
        Assert.Equal("https://news.ycombinator.com/item?id=primary", primary.Permalink);
        Assert.Equal(10, primary.Score);
        Assert.Equal(4, primary.CommentCount);
        Assert.Equal(createdAt, primary.CreatedUtc);
        var fallback = results[1];
        Assert.Equal("Fallback title", fallback.Title);
        Assert.Equal("Fallback content", fallback.Content);
        Assert.Equal("https://fallback.test", fallback.Url);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(unixTime).UtcDateTime, fallback.CreatedUtc);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"hits\":null}")]
    [InlineData("{\"hits\":[]}")]
    public async Task SearchAsync_WhenHitsAreMissingOrEmpty_ReturnsEmpty(string body)
    {
        var result = await CreateClient(JsonHandler(body))
            .SearchAsync(new HackerNewsSearchRequest { Query = "idea" });

        Assert.Empty(result);
    }

    [Fact]
    public async Task SearchAsync_WhenJsonIsMalformed_ThrowsJsonException()
    {
        var client = CreateClient(JsonHandler("{not-json"));

        await Assert.ThrowsAsync<JsonException>(
            () => client.SearchAsync(new HackerNewsSearchRequest { Query = "idea" }));
    }

    [Fact]
    public async Task SearchAsync_WhenHttpFails_PreservesStatusWithoutExposingBody()
    {
        const string sensitiveMarker = "SENSITIVE-HN-RESPONSE";
        var client = CreateClient(JsonHandler(sensitiveMarker, HttpStatusCode.BadGateway));

        var exception = await Assert.ThrowsAsync<HttpRequestException>(
            () => client.SearchAsync(new HackerNewsSearchRequest { Query = "idea" }));

        Assert.Equal(HttpStatusCode.BadGateway, exception.StatusCode);
        Assert.DoesNotContain(sensitiveMarker, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SearchAsync_WhenCallerCancels_PropagatesCancellation()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new TestHttpMessageHandler(async (_, token) =>
        {
            started.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            throw new InvalidOperationException("unreachable");
        });
        var client = CreateClient(handler);
        using var source = new CancellationTokenSource();

        var search = client.SearchAsync(new HackerNewsSearchRequest { Query = "idea" }, source.Token);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        source.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => search);
    }

    private static HackerNewsClient CreateClient(
        TestHttpMessageHandler handler,
        HackerNewsOptions? options = null)
        => new(new HttpClient(handler), options ?? new HackerNewsOptions { BaseUrl = "https://hn.test/api/v1" });

    private static TestHttpMessageHandler JsonHandler(
        string body,
        HttpStatusCode statusCode = HttpStatusCode.OK)
        => new((_, _) => Task.FromResult(TestHttpMessageHandler.JsonResponse(body, statusCode)));

    private static Dictionary<string, string> ParseQuery(Uri uri)
        => uri.Query.TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Split('=', 2))
            .ToDictionary(
                part => WebUtility.UrlDecode(part[0]),
                part => WebUtility.UrlDecode(part[1]));
}
