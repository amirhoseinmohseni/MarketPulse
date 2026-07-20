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
