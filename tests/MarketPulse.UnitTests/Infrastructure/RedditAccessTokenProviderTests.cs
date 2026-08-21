using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MarketPulse.Infrastructure.Reddit;

namespace MarketPulse.UnitTests.Infrastructure;

public class RedditAccessTokenProviderTests
{
    [Theory]
    [InlineData("ClientId")]
    [InlineData("ClientSecret")]
    [InlineData("AuthUrl")]
    [InlineData("UserAgent")]
    public async Task GetAccessToken_WhenConfigurationIsMissing_ThrowsBeforeHttpCall(string missing)
    {
        var handler = TokenHandler(RuntimeValue("token"), 3600);
        var provider = CreateProvider(handler, CreateOptions(missing));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => provider.GetAccessTokenAsync());
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task GetAccessToken_SendsExpectedRequestAndReturnsToken()
    {
        var options = CreateOptions();
        var expectedToken = RuntimeValue("token");
        string? authorization = null;
        string? userAgent = null;
        string? formBody = null;
        HttpMethod? method = null;
        Uri? uri = null;
        var handler = new TestHttpMessageHandler(async (request, cancellationToken) =>
        {
            authorization = request.Headers.Authorization?.ToString();
            userAgent = request.Headers.UserAgent.ToString();
            formBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            method = request.Method;
            uri = request.RequestUri;
            return TestHttpMessageHandler.JsonResponse(
                JsonSerializer.Serialize(new { access_token = expectedToken, expires_in = 3600 }));
        });
        var provider = CreateProvider(handler, options);

        var token = await provider.GetAccessTokenAsync();

        var expectedCredentials = Convert.ToBase64String(
            Encoding.UTF8.GetBytes(
                $"{Uri.EscapeDataString(options.ClientId)}:{Uri.EscapeDataString(options.ClientSecret)}"));
        Assert.Equal(expectedToken, token);
        Assert.Equal(HttpMethod.Post, method);
        Assert.Equal(options.AuthUrl, uri?.ToString());
        Assert.Equal($"Basic {expectedCredentials}", authorization);
        Assert.Equal(options.UserAgent, userAgent);
        Assert.Equal("grant_type=client_credentials", formBody);
    }

    [Fact]
    public async Task GetAccessToken_CachesTokenOutsideRefreshWindow()
    {
        var expectedToken = RuntimeValue("token");
        var handler = TokenHandler(expectedToken, 3600);
        var provider = CreateProvider(handler);

        var first = await provider.GetAccessTokenAsync();
        var second = await provider.GetAccessTokenAsync();

        Assert.Equal(expectedToken, first);
        Assert.Equal(first, second);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task GetAccessToken_WhenInsideRefreshWindow_RequestsNewToken()
    {
        var tokens = new[] { RuntimeValue("first"), RuntimeValue("second") };
        var call = 0;
        var handler = new TestHttpMessageHandler((_, _) =>
        {
            var token = tokens[Interlocked.Increment(ref call) - 1];
            return Task.FromResult(TestHttpMessageHandler.JsonResponse(
                JsonSerializer.Serialize(new { access_token = token, expires_in = 1 })));
        });
        var provider = CreateProvider(handler);

        var first = await provider.GetAccessTokenAsync();
        var second = await provider.GetAccessTokenAsync();

        Assert.Equal(tokens, new[] { first, second });
        Assert.Equal(2, handler.CallCount);
    }

    [Fact]
    public async Task GetAccessToken_ConcurrentCallsShareOneHttpRequest()
    {
        var expectedToken = RuntimeValue("token");
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new TestHttpMessageHandler(async (_, cancellationToken) =>
        {
            started.TrySetResult();
            await release.Task.WaitAsync(cancellationToken);
            return TestHttpMessageHandler.JsonResponse(
                JsonSerializer.Serialize(new { access_token = expectedToken, expires_in = 3600 }));
        });
        var provider = CreateProvider(handler);

        var calls = Enumerable.Range(0, 8)
            .Select(_ => provider.GetAccessTokenAsync())
            .ToArray();
        await started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        release.TrySetResult();
        var tokens = await Task.WhenAll(calls);

        Assert.All(tokens, token => Assert.Equal(expectedToken, token));
        Assert.Equal(1, handler.CallCount);
    }

    [Theory]
    [InlineData("{\"access_token\":null,\"expires_in\":3600}")]
    [InlineData("{\"access_token\":\"\",\"expires_in\":3600}")]
    [InlineData("{\"access_token\":\"value\",\"expires_in\":0}")]
    [InlineData("{\"access_token\":\"value\",\"expires_in\":-1}")]
    public async Task GetAccessToken_WhenPayloadValuesAreInvalid_RejectsResponse(string body)
    {
        var provider = CreateProvider(JsonHandler(body));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => provider.GetAccessTokenAsync());
    }

    [Fact]
    public async Task GetAccessToken_WhenJsonIsMalformed_ThrowsJsonException()
    {
        var provider = CreateProvider(JsonHandler("{not-json"));

        await Assert.ThrowsAsync<JsonException>(() => provider.GetAccessTokenAsync());
    }

    [Fact]
    public async Task GetAccessToken_WhenHttpFails_DoesNotExposeResponseOrCredentials()
    {
        var options = CreateOptions();
        const string sensitiveMarker = "SENSITIVE-REDDIT-AUTH-RESPONSE";
        var provider = CreateProvider(
            JsonHandler(sensitiveMarker, HttpStatusCode.Unauthorized),
            options);

        var exception = await Assert.ThrowsAsync<HttpRequestException>(
            () => provider.GetAccessTokenAsync());

        Assert.Equal(HttpStatusCode.Unauthorized, exception.StatusCode);
        Assert.DoesNotContain(sensitiveMarker, exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(options.ClientSecret, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetAccessToken_CancellationWhileWaitingForLockIsPropagated()
    {
        var expectedToken = RuntimeValue("token");
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new TestHttpMessageHandler(async (_, cancellationToken) =>
        {
            started.TrySetResult();
            await release.Task.WaitAsync(cancellationToken);
            return TestHttpMessageHandler.JsonResponse(
                JsonSerializer.Serialize(new { access_token = expectedToken, expires_in = 3600 }));
        });
        var provider = CreateProvider(handler);
        var first = provider.GetAccessTokenAsync();
        await started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        using var source = new CancellationTokenSource();
        var waiting = provider.GetAccessTokenAsync(source.Token);
        source.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting);
        release.TrySetResult();
        Assert.Equal(expectedToken, await first);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task GetAccessToken_CancellationDuringHttpIsPropagated()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new TestHttpMessageHandler(async (_, cancellationToken) =>
        {
            started.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("unreachable");
        });
        var provider = CreateProvider(handler);
        using var source = new CancellationTokenSource();

        var request = provider.GetAccessTokenAsync(source.Token);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        source.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => request);
    }

    private static RedditAccessTokenProvider CreateProvider(
        TestHttpMessageHandler handler,
        RedditOptions? options = null)
        => new(
            new StubHttpClientFactory(new HttpClient(handler)),
            options ?? CreateOptions());

    private static RedditOptions CreateOptions(string? missing = null)
    {
        var clientId = RuntimeValue("client");
        var clientSecret = RuntimeValue("secret");
        return new RedditOptions
        {
            ClientId = missing == "ClientId" ? "" : clientId,
            ClientSecret = missing == "ClientSecret" ? "" : clientSecret,
            AuthUrl = missing == "AuthUrl" ? "" : "https://reddit-auth.test/token",
            UserAgent = missing == "UserAgent" ? "" : "MarketPulse.Tests/1.0",
            BaseUrl = "https://reddit.test"
        };
    }

    private static TestHttpMessageHandler TokenHandler(string token, int expiresIn)
        => JsonHandler(JsonSerializer.Serialize(new { access_token = token, expires_in = expiresIn }));

    private static TestHttpMessageHandler JsonHandler(
        string body,
        HttpStatusCode statusCode = HttpStatusCode.OK)
        => new((_, _) => Task.FromResult(TestHttpMessageHandler.JsonResponse(body, statusCode)));

    private static string RuntimeValue(string prefix)
        => $"{prefix}-{Convert.ToHexString(RandomNumberGenerator.GetBytes(16))}";

    private sealed class StubHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }
}
