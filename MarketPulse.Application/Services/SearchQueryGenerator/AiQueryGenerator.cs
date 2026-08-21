using MarketPulse.Domain.Entities;
using MarketPulse.Domain.Enums;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MarketPulse.Application.Services.SearchQueryGenerator
{
    public class AiQueryGenerator : ISearchQueryGenerator
    {
        private const int MinimumQueryCount = 8;
        private const int MaximumQueryCount = 12;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = false,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
        };

        private readonly IAiSearchQueryClient _aiClient;

        public AiQueryGenerator(IAiSearchQueryClient aiClient)
        {
            _aiClient = aiClient;
        }

        public async Task<List<SearchQuery>> GenerateAsync(string idea, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(idea))
            {
                return [];
            }

            var prompt = BuildPrompt(idea);
            var response = await _aiClient.GenerateAsync(prompt, cancellationToken);

            return ParseSearchQueries(response);
        }

        private static AiSearchQueryPrompt BuildPrompt(string idea)
        {
            const string systemPrompt = """
                You generate concise, source-neutral keyword search queries for validating startup and product ideas.
                The queries must work well with simple search APIs such as Hacker News Algolia, not only Google-style web search.
                Return only valid JSON. Do not include markdown, explanations, or comments.
                """;

            var userPrompt = $$"""
                Generate 8 to 12 source-neutral search queries for this product idea:
                "{{idea.Trim()}}"

                Cover these categories:
                - Problem: short keyword queries about pain points, unmet needs, failure modes
                - Competitor: short keyword queries to discover direct or indirect competitors
                - Solution: short keyword queries about existing solutions, alternatives, tools, products
                - Discussion: short keyword queries likely to match product discussions and technical conversations

                Query writing rules:
                - Prefer concise topic or keyword queries, usually 2 to 5 words.
                - Make queries source-neutral and suitable for search APIs such as Hacker News Algolia.
                - Do not write full sentences.
                - Do not use Google search operators or syntax: site:, intitle:, inurl:, OR, quoted phrases.
                - Do not include source names unless they are genuinely part of the product idea: reddit, subreddit, forum, hacker news, product hunt.
                - Avoid long phrases such as "best alternatives to", "user complaints about", "pain points with".
                - Prefer core concepts, user segments, workflows, technologies, and problem keywords.
                - Include at least one query from every category.
                - Do not repeat a query, including with different letter casing.

                Return this JSON shape:
                {
                  "queries": [
                    {
                      "query": "exact search query text",
                      "category": "Problem | Competitor | Solution | Discussion",
                      "priority": 1
                    }
                  ]
                }

                Priority must be between 1 and 5, where 1 is highest priority.
                """;

            return new AiSearchQueryPrompt(
                systemPrompt,
                userPrompt,
                "search_queries",
                BuildResponseSchema());
        }

        private static List<SearchQuery> ParseSearchQueries(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new InvalidAiSearchQueryResponseException(
                    "The AI search-query response was empty.");
            }

            AiSearchQueryResponse response;
            try
            {
                response = JsonSerializer.Deserialize<AiSearchQueryResponse>(json, JsonOptions)
                    ?? throw new InvalidAiSearchQueryResponseException(
                        "The AI search-query response was null.");
            }
            catch (JsonException exception)
            {
                throw new InvalidAiSearchQueryResponseException(
                    "The AI response did not match the required search-query JSON structure.",
                    exception);
            }

            if (response.Queries is null || response.Queries.Any(item => item is null))
            {
                throw new InvalidAiSearchQueryResponseException(
                    "The AI response did not match the required search-query JSON structure.");
            }

            var queries = new List<SearchQuery>(response.Queries.Count);
            var seenQueries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var item in response.Queries)
            {
                if (item!.Query is null || item.Category is null)
                {
                    throw new InvalidAiSearchQueryResponseException(
                        "The AI response did not match the required search-query JSON structure.");
                }

                var queryText = item.Query.Trim();
                if (string.IsNullOrWhiteSpace(queryText)
                    || item.Priority is < 1 or > 5
                    || !TryParseCategory(item.Category, out var category)
                    || !seenQueries.Add(queryText))
                {
                    continue;
                }

                queries.Add(new SearchQuery
                {
                    Id = Guid.NewGuid(),
                    Query = queryText,
                    Category = category,
                    Priority = item.Priority
                });
            }

            ValidateFinalQuerySet(queries);
            return queries;
        }

        private static void ValidateFinalQuerySet(IReadOnlyCollection<SearchQuery> queries)
        {
            if (queries.Count is < MinimumQueryCount or > MaximumQueryCount)
            {
                throw new InvalidAiSearchQueryResponseException(
                    $"The AI response must contain between {MinimumQueryCount} and {MaximumQueryCount} valid unique queries.");
            }

            var missingCategories = Enum.GetValues<QueryCategory>()
                .Where(category => queries.All(query => query.Category != category))
                .ToArray();

            if (missingCategories.Length > 0)
            {
                throw new InvalidAiSearchQueryResponseException(
                    "The AI response must contain at least one valid query from every category.");
            }
        }

        private static bool TryParseCategory(
            string category,
            out QueryCategory parsedCategory)
        {
            parsedCategory = category switch
            {
                "Problem" => QueryCategory.Problem,
                "Competitor" => QueryCategory.Competitor,
                "Solution" => QueryCategory.Solution,
                "Discussion" => QueryCategory.Discussion,
                _ => default
            };

            return category is "Problem" or "Competitor" or "Solution" or "Discussion";
        }

        private static string BuildResponseSchema()
            => $$"""
                {
                  "type": "object",
                  "additionalProperties": false,
                  "required": ["queries"],
                  "properties": {
                    "queries": {
                      "type": "array",
                      "minItems": {{MinimumQueryCount}},
                      "maxItems": {{MaximumQueryCount}},
                      "items": {
                        "type": "object",
                        "additionalProperties": false,
                        "required": ["query", "category", "priority"],
                        "properties": {
                          "query": { "type": "string" },
                          "category": {
                            "type": "string",
                            "enum": ["Problem", "Competitor", "Solution", "Discussion"]
                          },
                          "priority": { "type": "integer", "minimum": 1, "maximum": 5 }
                        }
                      }
                    }
                  }
                }
                """;

        private sealed class AiSearchQueryResponse
        {
            [JsonPropertyName("queries")]
            public required List<AiSearchQueryItem?> Queries { get; init; }
        }

        private sealed class AiSearchQueryItem
        {
            [JsonPropertyName("query")]
            public required string? Query { get; init; }

            [JsonPropertyName("category")]
            public required string? Category { get; init; }

            [JsonPropertyName("priority")]
            public required int Priority { get; init; }
        }
    }
}
