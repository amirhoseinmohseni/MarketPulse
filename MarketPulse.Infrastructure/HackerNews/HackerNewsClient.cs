using MarketPulse.Application.Services.HackerNewsDataCollection;
using System.Net;
using System.Text.Json;

namespace MarketPulse.Infrastructure.HackerNews
{
    public sealed class HackerNewsClient : IHackerNewsClient
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        private readonly HttpClient _httpClient;
        private readonly HackerNewsOptions _options;

        public HackerNewsClient(
            HttpClient httpClient,
            HackerNewsOptions options)
        {
            _httpClient = httpClient;
            _options = options;
        }

        public async Task<IReadOnlyList<HackerNewsSearchResult>> SearchAsync(
            HackerNewsSearchRequest request,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(request.Query))
            {
                throw new ArgumentException("Hacker News search query cannot be empty.", nameof(request));
            }

            using var httpRequest = new HttpRequestMessage(HttpMethod.Get, BuildSearchUri(request));
            using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(
                    $"Hacker News search request failed with status code {(int)response.StatusCode} ({response.StatusCode}).",
                    null,
                    response.StatusCode);
            }

            var searchResponse = JsonSerializer.Deserialize<AlgoliaHackerNewsResponse>(responseBody, JsonOptions);

            if (searchResponse?.Hits is null || searchResponse.Hits.Count == 0)
            {
                return Array.Empty<HackerNewsSearchResult>();
            }

            return searchResponse.Hits
                .Where(x => !string.IsNullOrWhiteSpace(x.ObjectId))
                .Select(MapHit)
                .Where(x => !string.IsNullOrWhiteSpace(x.Title))
                .ToList();
        }

        private Uri BuildSearchUri(HackerNewsSearchRequest request)
        {
            var baseUrl = _options.BaseUrl.TrimEnd('/');
            var limit = request.Limit > 0
                ? request.Limit
                : _options.DefaultLimit;

            var queryParameters = new Dictionary<string, string>
            {
                ["query"] = request.Query,
                ["tags"] = _options.Tags,
                ["hitsPerPage"] = limit.ToString()
            };

            var queryString = string.Join("&", queryParameters
                .Where(x => !string.IsNullOrWhiteSpace(x.Value))
                .Select(x => $"{WebUtility.UrlEncode(x.Key)}={WebUtility.UrlEncode(x.Value)}"));

            return new Uri($"{baseUrl}/search?{queryString}");
        }

        private static HackerNewsSearchResult MapHit(AlgoliaHackerNewsHit hit)
        {
            var title = !string.IsNullOrWhiteSpace(hit.Title)
                ? hit.Title
                : hit.StoryTitle ?? string.Empty;
            var content = !string.IsNullOrWhiteSpace(hit.StoryText)
                ? hit.StoryText
                : hit.CommentText;
            var createdUtc = hit.CreatedAt
                ?? (hit.CreatedAtUnix.HasValue
                    ? DateTimeOffset.FromUnixTimeSeconds(hit.CreatedAtUnix.Value).UtcDateTime
                    : null);

            return new HackerNewsSearchResult
            {
                ExternalId = hit.ObjectId,
                Title = title,
                Content = content,
                Url = !string.IsNullOrWhiteSpace(hit.Url) ? hit.Url : hit.StoryUrl,
                Permalink = $"https://news.ycombinator.com/item?id={hit.ObjectId}",
                Score = hit.Points,
                CommentCount = hit.CommentCount,
                CreatedUtc = createdUtc
            };
        }
    }
}
