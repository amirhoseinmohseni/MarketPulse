using MarketPulse.Application.Services.RedditDataCollection;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace MarketPulse.Infrastructure.Reddit
{
    public sealed class RedditClient : IRedditClient
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        private readonly HttpClient _httpClient;
        private readonly RedditOptions _options;
        private readonly IRedditAccessTokenProvider _accessTokenProvider;

        public RedditClient(
            HttpClient httpClient,
            RedditOptions options,
            IRedditAccessTokenProvider accessTokenProvider)
        {
            _httpClient = httpClient;
            _options = options;
            _accessTokenProvider = accessTokenProvider;
        }

        public async Task<IReadOnlyList<RedditPostSearchResult>> SearchPostsAsync(
            RedditPostSearchRequest request,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(request.Query))
            {
                throw new ArgumentException("Reddit search query cannot be empty.", nameof(request));
            }

            var accessToken = await _accessTokenProvider.GetAccessTokenAsync(cancellationToken);

            using var httpRequest = new HttpRequestMessage(HttpMethod.Get, BuildSearchUri(request));
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            httpRequest.Headers.UserAgent.ParseAdd(_options.UserAgent);

            using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw CreateRedditException(response.StatusCode, responseBody);
            }

            var listing = JsonSerializer.Deserialize<RedditListingResponse>(responseBody, JsonOptions);
            var children = listing?.Data?.Children;

            if (children is null || children.Count == 0)
            {
                return Array.Empty<RedditPostSearchResult>();
            }

            return children
                .Where(child => child.Data is not null)
                .Select(child => MapPost(child.Data!))
                .Where(post => !string.IsNullOrWhiteSpace(post.RedditPostId))
                .ToList();
        }

        private Uri BuildSearchUri(RedditPostSearchRequest request)
        {
            var baseUrl = _options.BaseUrl.TrimEnd('/');
            var path = string.IsNullOrWhiteSpace(request.Subreddit)
                ? "/search"
                : $"/r/{Uri.EscapeDataString(request.Subreddit.Trim())}/search";

            var queryParameters = new Dictionary<string, string>
            {
                ["q"] = request.Query,
                ["limit"] = ResolveLimit(request).ToString(),
                ["sort"] = request.Sort,
                ["t"] = request.TimeRange,
                ["type"] = "link"
            };

            if (!string.IsNullOrWhiteSpace(request.Subreddit))
            {
                queryParameters["restrict_sr"] = "true";
            }

            var queryString = string.Join("&", queryParameters
                .Where(x => !string.IsNullOrWhiteSpace(x.Value))
                .Select(x => $"{WebUtility.UrlEncode(x.Key)}={WebUtility.UrlEncode(x.Value)}"));

            return new Uri($"{baseUrl}{path}?{queryString}");
        }

        private int ResolveLimit(RedditPostSearchRequest request)
        {
            if (request.Limit > 0)
            {
                return request.Limit;
            }

            return _options.DefaultLimit > 0
                ? _options.DefaultLimit
                : 25;
        }

        private static RedditPostSearchResult MapPost(RedditPostData data)
            => new()
            {
                RedditPostId = string.IsNullOrWhiteSpace(data.Id) ? data.Name : data.Id,
                Subreddit = data.Subreddit,
                Title = data.Title,
                SelfText = data.SelfText,
                Url = data.Url,
                Permalink = data.Permalink,
                Score = data.Score,
                CommentCount = data.CommentCount,
                CreatedUtc = DateTimeOffset
                    .FromUnixTimeSeconds((long)data.CreatedUtc)
                    .UtcDateTime
            };

        private static HttpRequestException CreateRedditException(
            HttpStatusCode statusCode,
            string responseBody)
        {
            return new HttpRequestException(
                $"Reddit search request failed with status code {(int)statusCode} ({statusCode}). Response body: {responseBody}",
                null,
                statusCode);
        }
    }
}
