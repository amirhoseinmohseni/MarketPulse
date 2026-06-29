using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace MarketPulse.Infrastructure.Reddit
{
    public sealed class RedditAccessTokenProvider : IRedditAccessTokenProvider
    {
        public const string HttpClientName = "RedditAuth";

        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
        private static readonly TimeSpan RefreshSkew = TimeSpan.FromSeconds(60);

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly RedditOptions _options;
        private readonly SemaphoreSlim _tokenLock = new(1, 1);

        private string? _accessToken;
        private DateTimeOffset _expiresAt = DateTimeOffset.MinValue;

        public RedditAccessTokenProvider(
            IHttpClientFactory httpClientFactory,
            RedditOptions options)
        {
            _httpClientFactory = httpClientFactory;
            _options = options;
        }

        public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)
        {
            if (HasValidToken())
            {
                return _accessToken!;
            }

            await _tokenLock.WaitAsync(cancellationToken);
            try
            {
                if (HasValidToken())
                {
                    return _accessToken!;
                }

                var tokenResponse = await RequestAccessTokenAsync(cancellationToken);

                _accessToken = tokenResponse.AccessToken;
                _expiresAt = DateTimeOffset.UtcNow
                    .AddSeconds(tokenResponse.ExpiresIn)
                    .Subtract(RefreshSkew);

                return _accessToken;
            }
            finally
            {
                _tokenLock.Release();
            }
        }

        private bool HasValidToken()
            => !string.IsNullOrWhiteSpace(_accessToken)
                && DateTimeOffset.UtcNow < _expiresAt;

        private async Task<RedditTokenResponse> RequestAccessTokenAsync(CancellationToken cancellationToken)
        {
            EnsureConfigured();

            using var request = new HttpRequestMessage(HttpMethod.Post, _options.AuthUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", CreateBasicAuthValue());
            request.Headers.UserAgent.ParseAdd(_options.UserAgent);
            request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials"
            });

            var httpClient = _httpClientFactory.CreateClient(HttpClientName);
            using var response = await httpClient.SendAsync(request, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw CreateRedditAuthException(response.StatusCode, responseBody);
            }

            var tokenResponse = JsonSerializer.Deserialize<RedditTokenResponse>(responseBody, JsonOptions);

            if (tokenResponse is null || string.IsNullOrWhiteSpace(tokenResponse.AccessToken))
            {
                throw new InvalidOperationException("Reddit token response did not include an access token.");
            }

            if (tokenResponse.ExpiresIn <= 0)
            {
                throw new InvalidOperationException("Reddit token response included an invalid expires_in value.");
            }

            return tokenResponse;
        }

        private void EnsureConfigured()
        {
            if (string.IsNullOrWhiteSpace(_options.ClientId))
            {
                throw new InvalidOperationException("Reddit client ID is not configured. Set Reddit:ClientId.");
            }

            if (string.IsNullOrWhiteSpace(_options.ClientSecret))
            {
                throw new InvalidOperationException("Reddit client secret is not configured. Set Reddit:ClientSecret.");
            }

            if (string.IsNullOrWhiteSpace(_options.AuthUrl))
            {
                throw new InvalidOperationException("Reddit auth URL is not configured. Set Reddit:AuthUrl.");
            }

            if (string.IsNullOrWhiteSpace(_options.UserAgent))
            {
                throw new InvalidOperationException("Reddit user agent is not configured. Set Reddit:UserAgent.");
            }
        }

        private string CreateBasicAuthValue()
        {
            var credentials = $"{Uri.EscapeDataString(_options.ClientId)}:{Uri.EscapeDataString(_options.ClientSecret)}";
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(credentials));
        }

        private static HttpRequestException CreateRedditAuthException(
            HttpStatusCode statusCode,
            string responseBody)
        {
            return new HttpRequestException(
                $"Reddit token request failed with status code {(int)statusCode} ({statusCode}). Response body: {responseBody}",
                null,
                statusCode);
        }
    }
}
