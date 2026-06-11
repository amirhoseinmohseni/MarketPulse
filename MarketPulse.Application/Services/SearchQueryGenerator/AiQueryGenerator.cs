using MarketPulse.Domain.Entities;
using MarketPulse.Domain.Enums;
using System.Text.Json;

namespace MarketPulse.Application.Services.SearchQueryGenerator
{
    public class AiQueryGenerator : ISearchQueryGenerator
    {
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
                You generate practical web search queries for validating startup and product ideas.
                Return only valid JSON. Do not include markdown, explanations, or comments.
                """;

            var userPrompt = $$"""
                Generate 8 to 12 search queries for this product idea:
                "{{idea.Trim()}}"

                Cover these categories:
                - Problem: searches about pain points, user complaints, unmet needs
                - Competitor: searches to discover direct or indirect competitors
                - Solution: searches about existing solutions, alternatives, tools, products
                - Discussion: searches for real user discussions, forums, Reddit, communities

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

            return new AiSearchQueryPrompt(systemPrompt, userPrompt);
        }

        private static List<SearchQuery> ParseSearchQueries(string json)
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            var queriesElement = root.ValueKind == JsonValueKind.Array
                ? root
                : root.GetProperty("queries");

            var queries = new List<SearchQuery>();

            foreach (var item in queriesElement.EnumerateArray())
            {
                queries.Add(new SearchQuery
                {
                    Id = Guid.NewGuid(),
                    Query = item.GetProperty("query").GetString()!,
                    Category = ParseCategory(item.GetProperty("category")),
                    Priority = item.GetProperty("priority").GetInt32()
                });
            }

            return queries;
        }

        private static QueryCategory ParseCategory(JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Number)
            {
                return (QueryCategory)element.GetInt32();
            }

            return Enum.Parse<QueryCategory>(element.GetString()!, ignoreCase: true);
        }
    }
}
