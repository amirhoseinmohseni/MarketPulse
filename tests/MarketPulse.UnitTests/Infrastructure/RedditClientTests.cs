using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using MarketPulse.Application.Services.RedditDataCollection;
using MarketPulse.Infrastructure.Reddit;

namespace MarketPulse.UnitTests.Infrastructure;

public class RedditClientTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SearchPosts_WhenQueryIsEmpty_ThrowsBeforeTokenAndHttp(string query)
    {
        var tokenProvider = new StubTokenProvider(RuntimeValue("token"));
        var handler = JsonHandler("{}");
        var client = CreateClient(handler, tokenProvider);

        await Assert.ThrowsAsync<ArgumentException>(
            () => client.SearchPostsAsync(new RedditPostSearchRequest { Query = query }));
        Assert.Equal(0, tokenProvider.CallCount);
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task SearchPosts_BuildsGeneralSearchWithAuthAndParameters()
    {
        var token = RuntimeValue("token");
        var tokenProvider = new StubTokenProvider(token);
        Uri? uri = null;
        string? authorization = null;
        string? userAgent = null;
        var handler = new TestHttpMessageHandler((request, _) =>
        {
            uri = request.RequestUri;
            authorization = request.Headers.Authorization?.ToString();
            userAgent = request.Headers.UserAgent.ToString();
            return Task.FromResult(TestHttpMessageHandler.JsonResponse("{}"));
        });
        var client = CreateClient(handler, tokenProvider);

        await client.SearchPostsAsync(new RedditPostSearchRequest
        {
            Query = "C# billing & tools",
            Limit = 11,
            Sort = "new",
            TimeRange = "month"
        });

        Assert.Equal("/search", uri?.AbsolutePath);
        var parameters = ParseQuery(uri!);
        Assert.Equal("C# billing & tools", parameters["q"]);
        Assert.Equal("11", parameters["limit"]);
        Assert.Equal("new", parameters["sort"]);
        Assert.Equal("month", parameters["t"]);
        Assert.Equal("link", parameters["type"]);
        Assert.False(parameters.ContainsKey("restrict_sr"));
        Assert.Equal($"Bearer {token}", authorization);
        Assert.Equal("MarketPulse.Tests/1.0", userAgent);
    }

    [Fact]
    public async Task SearchPosts_WithSubreddit_UsesEncodedScopedPath()
    {
        Uri? uri = null;
        var handler = new TestHttpMessageHandler((request, _) =>
        {
            uri = request.RequestUri;
            return Task.FromResult(TestHttpMessageHandler.JsonResponse("{}"));
        });
        var client = CreateClient(handler, new StubTokenProvider(RuntimeValue("token")));

        await client.SearchPostsAsync(new RedditPostSearchRequest
        {
            Query = "idea",
            Subreddit = " dot net "
        });

        Assert.Equal("/r/dot%20net/search", uri?.AbsolutePath);
        Assert.Equal("true", ParseQuery(uri!)["restrict_sr"]);
    }

    [Theory]
    [InlineData(33, 19, 33)]
    [InlineData(0, 19, 19)]
    [InlineData(0, 0, 25)]
    public async Task SearchPosts_ResolvesLimit(int requested, int configured, int expected)
    {
        Uri? uri = null;
        var handler = new TestHttpMessageHandler((request, _) =>
        {
            uri = request.RequestUri;
            return Task.FromResult(TestHttpMessageHandler.JsonResponse("{}"));
        });
        var client = CreateClient(
            handler,
            new StubTokenProvider(RuntimeValue("token")),
            new RedditOptions
            {
                BaseUrl = "https://reddit.test",
                UserAgent = "MarketPulse.Tests/1.0",
                DefaultLimit = configured
            });

        await client.SearchPostsAsync(new RedditPostSearchRequest { Query = "idea", Limit = requested });

        Assert.Equal(expected.ToString(), ParseQuery(uri!)["limit"]);
    }

    [Fact]
    public async Task SearchPosts_MapsFieldsAndFiltersInvalidChildren()
    {
        const long unixTime = 1_775_000_000;
        var body = JsonSerializer.Serialize(new
        {
            data = new
            {
                children = new object?[]
                {
                    new
                    {
                        data = new
                        {
                            id = "primary",
                            name = "ignored",
                            subreddit = "startups",
                            title = "Primary",
                            selftext = "Content",
                            url = "https://reddit.test/post",
                            permalink = "/r/startups/comments/primary",
                            score = 12,
                            num_comments = 5,
                            created_utc = unixTime
                        }
                    },
                    new
                    {
                        data = new
                        {
                            id = "",
                            name = "t3_fallback",
                            subreddit = "dotnet",
                            title = "Fallback",
                            selftext = (string?)null,
                            url = (string?)null,
                            permalink = "/r/dotnet/comments/fallback",
                            score = 3,
                            num_comments = 1,
                            created_utc = unixTime
                        }
                    },
                    new { data = (object?)null },
                    new { data = new { id = "", name = "", created_utc = unixTime } }
                }
            }
        });
        var client = CreateClient(JsonHandler(body), new StubTokenProvider(RuntimeValue("token")));

        var results = await client.SearchPostsAsync(new RedditPostSearchRequest { Query = "idea" });

        Assert.Equal(2, results.Count);
        var primary = results[0];
        Assert.Equal("primary", primary.RedditPostId);
        Assert.Equal("startups", primary.Subreddit);
        Assert.Equal("Primary", primary.Title);
        Assert.Equal("Content", primary.SelfText);
        Assert.Equal("https://reddit.test/post", primary.Url);
        Assert.Equal("/r/startups/comments/primary", primary.Permalink);
        Assert.Equal(12, primary.Score);
        Assert.Equal(5, primary.CommentCount);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(unixTime).UtcDateTime, primary.CreatedUtc);
        Assert.Equal("t3_fallback", results[1].RedditPostId);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"data\":null}")]
    [InlineData("{\"data\":{\"children\":null}}")]
    [InlineData("{\"data\":{\"children\":[]}}")]
    public async Task SearchPosts_WhenListingIsMissingOrEmpty_ReturnsEmpty(string body)
    {
        var results = await CreateClient(
                JsonHandler(body),
                new StubTokenProvider(RuntimeValue("token")))
            .SearchPostsAsync(new RedditPostSearchRequest { Query = "idea" });

        Assert.Empty(results);
    }

    [Fact]
    public async Task SearchPosts_WhenJsonIsMalformed_ThrowsJsonException()
    {
        var client = CreateClient(
            JsonHandler("{not-json"),
            new StubTokenProvider(RuntimeValue("token")));

        await Assert.ThrowsAsync<JsonException>(
            () => client.SearchPostsAsync(new RedditPostSearchRequest { Query = "idea" }));
    }

    [Fact]
    public async Task SearchPosts_WhenHttpFails_PreservesStatusWithoutExposingBodyOrToken()
    {
        var token = RuntimeValue("token");
        const string sensitiveMarker = "SENSITIVE-REDDIT-RESPONSE";
        var client = CreateClient(
            JsonHandler(sensitiveMarker, HttpStatusCode.BadGateway),
            new StubTokenProvider(token));

        var exception = await Assert.ThrowsAsync<HttpRequestException>(
            () => client.SearchPostsAsync(new RedditPostSearchRequest { Query = "idea" }));

        Assert.Equal(HttpStatusCode.BadGateway, exception.StatusCode);
        Assert.DoesNotContain(sensitiveMarker, exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(token, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SearchPosts_PropagatesCancellationToTokenProvider()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();
        var tokenProvider = new StubTokenProvider
        {
            Handler = token => Task.FromCanceled<string>(token)
        };
        var client = CreateClient(JsonHandler("{}"), tokenProvider);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => client.SearchPostsAsync(
                new RedditPostSearchRequest { Query = "idea" },
                source.Token));
    }

    [Fact]
    public async Task SearchPosts_PropagatesCancellationToHttpClient()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new TestHttpMessageHandler(async (_, token) =>
        {
            started.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            throw new InvalidOperationException("unreachable");
        });
        var client = CreateClient(handler, new StubTokenProvider(RuntimeValue("token")));
        using var source = new CancellationTokenSource();

        var search = client.SearchPostsAsync(
            new RedditPostSearchRequest { Query = "idea" },
            source.Token);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        source.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => search);
    }

    private static RedditClient CreateClient(
        TestHttpMessageHandler handler,
        StubTokenProvider tokenProvider,
        RedditOptions? options = null)
        => new(
            new HttpClient(handler),
            options ?? new RedditOptions
            {
                BaseUrl = "https://reddit.test/",
                UserAgent = "MarketPulse.Tests/1.0",
                DefaultLimit = 25
            },
            tokenProvider);

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

    private static string RuntimeValue(string prefix)
        => $"{prefix}-{Convert.ToHexString(RandomNumberGenerator.GetBytes(16))}";

    private sealed class StubTokenProvider : IRedditAccessTokenProvider
    {
        private readonly string? _token;
        public StubTokenProvider() { }
        public StubTokenProvider(string token) => _token = token;
        public int CallCount { get; private set; }
        public Func<CancellationToken, Task<string>>? Handler { get; init; }

        public Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Handler?.Invoke(cancellationToken) ?? Task.FromResult(_token!);
        }
    }
}
