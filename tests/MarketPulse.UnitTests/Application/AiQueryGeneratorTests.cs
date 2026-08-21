using System.Text.Json;
using MarketPulse.Application.Services.SearchQueryGenerator;
using MarketPulse.Domain.Enums;

namespace MarketPulse.UnitTests.Application;

public class AiQueryGeneratorTests
{
    [Fact]
    public async Task GenerateAsync_TrimsQueriesAndRemovesCaseInsensitiveDuplicates()
    {
        var client = new StubAiSearchQueryClient(CreateJson(
            Query("  payment delays  ", "Problem", 1),
            Query("Stripe", "Competitor", 1),
            Query("invoice automation", "Solution", 1),
            Query("billing workflows", "Discussion", 2),
            Query("late invoice pain", "Problem", 2),
            Query("payment platforms", "Competitor", 2),
            Query("accounts receivable tools", "Solution", 3),
            Query("founder billing discussion", "Discussion", 3),
            Query("PAYMENT DELAYS", "Problem", 4)));
        var generator = new AiQueryGenerator(client);

        var result = await generator.GenerateAsync("Automated startup billing");

        Assert.Equal(8, result.Count);
        Assert.Equal("payment delays", result[0].Query);
        Assert.Equal(8, result.Select(query => query.Id).Distinct().Count());
        Assert.Equal(
            8,
            result.Select(query => query.Query)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count());
        Assert.All(
            Enum.GetValues<QueryCategory>(),
            category => Assert.Contains(result, query => query.Category == category));
    }

    [Fact]
    public async Task GenerateAsync_RemovesOnlySemanticallyInvalidQueries()
    {
        var client = new StubAiSearchQueryClient(CreateJson(
            ValidQueries()
                .Concat(
                [
                    Query("   ", "Problem", 1),
                    Query("invalid category", "Other", 1),
                    Query("invalid priority", "Solution", 6),
                    Query("problem one", "Discussion", 2)
                ])
                .ToArray()));
        var generator = new AiQueryGenerator(client);

        var result = await generator.GenerateAsync("An idea");

        Assert.Equal(8, result.Count);
        Assert.DoesNotContain(result, query => query.Query.Contains("invalid"));
    }

    [Fact]
    public async Task GenerateAsync_WhenFilteringLeavesFewerThanEight_Throws()
    {
        var queries = ValidQueries().ToArray();
        queries[^1] = Query("PROBLEM ONE", "Discussion", 2);
        var generator = new AiQueryGenerator(
            new StubAiSearchQueryClient(CreateJson(queries)));

        var exception = await Assert.ThrowsAsync<InvalidAiSearchQueryResponseException>(
            () => generator.GenerateAsync("An idea"));

        Assert.Contains("between 8 and 12", exception.Message);
    }

    [Fact]
    public async Task GenerateAsync_WhenARequiredCategoryIsMissing_Throws()
    {
        var queries = ValidQueries()
            .Select(item => item.Category == "Discussion"
                ? Query(item.Query, "Problem", item.Priority)
                : item)
            .ToArray();
        var generator = new AiQueryGenerator(
            new StubAiSearchQueryClient(CreateJson(queries)));

        var exception = await Assert.ThrowsAsync<InvalidAiSearchQueryResponseException>(
            () => generator.GenerateAsync("An idea"));

        Assert.Contains("every category", exception.Message);
    }

    [Fact]
    public async Task GenerateAsync_WhenMoreThanTwelveValidQueriesAreReturned_Throws()
    {
        var queries = ValidQueries()
            .Concat(
                Enumerable.Range(1, 5)
                    .Select(index => Query($"extra query {index}", "Problem", 3)))
            .ToArray();
        var generator = new AiQueryGenerator(
            new StubAiSearchQueryClient(CreateJson(queries)));

        await Assert.ThrowsAsync<InvalidAiSearchQueryResponseException>(
            () => generator.GenerateAsync("An idea"));
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("{\"queries\":[],\"extra\":true}")]
    [InlineData("{\"queries\":[{\"query\":\"one\",\"category\":\"Problem\",\"priority\":1,\"extra\":true}]}")]
    [InlineData("{\"queries\":[{\"query\":\"one\",\"category\":\"Problem\"}]}")]
    [InlineData("{\"Queries\":[]}")]
    public async Task GenerateAsync_WhenJsonStructureIsNotExact_Throws(string response)
    {
        var generator = new AiQueryGenerator(new StubAiSearchQueryClient(response));

        await Assert.ThrowsAsync<InvalidAiSearchQueryResponseException>(
            () => generator.GenerateAsync("An idea"));
    }

    [Fact]
    public async Task GenerateAsync_SuppliesStrictBoundedResponseSchema()
    {
        var client = new StubAiSearchQueryClient(CreateJson(ValidQueries().ToArray()));
        var generator = new AiQueryGenerator(client);

        await generator.GenerateAsync("An idea");

        var prompt = Assert.IsType<AiSearchQueryPrompt>(client.CapturedPrompt);
        Assert.Equal("search_queries", prompt.ResponseSchemaName);
        using var schema = JsonDocument.Parse(prompt.ResponseJsonSchema);
        var root = schema.RootElement;
        Assert.False(root.GetProperty("additionalProperties").GetBoolean());
        Assert.Equal(
            new[] { "queries" },
            root.GetProperty("required")
                .EnumerateArray()
                .Select(item => item.GetString()));

        var queries = root.GetProperty("properties").GetProperty("queries");
        Assert.Equal(8, queries.GetProperty("minItems").GetInt32());
        Assert.Equal(12, queries.GetProperty("maxItems").GetInt32());

        var item = queries.GetProperty("items");
        Assert.False(item.GetProperty("additionalProperties").GetBoolean());
        Assert.Equal(
            new[] { "query", "category", "priority" },
            item.GetProperty("required")
                .EnumerateArray()
                .Select(property => property.GetString()));
        Assert.Equal(
            new[] { "Problem", "Competitor", "Solution", "Discussion" },
            item.GetProperty("properties")
                .GetProperty("category")
                .GetProperty("enum")
                .EnumerateArray()
                .Select(category => category.GetString()));
    }

    private static IEnumerable<QueryResponseItem> ValidQueries()
    {
        yield return Query("problem one", "Problem", 1);
        yield return Query("competitor one", "Competitor", 1);
        yield return Query("solution one", "Solution", 1);
        yield return Query("discussion one", "Discussion", 1);
        yield return Query("problem two", "Problem", 2);
        yield return Query("competitor two", "Competitor", 2);
        yield return Query("solution two", "Solution", 2);
        yield return Query("discussion two", "Discussion", 2);
    }

    private static QueryResponseItem Query(string query, string category, int priority)
        => new(query, category, priority);

    private static string CreateJson(params QueryResponseItem[] queries)
        => JsonSerializer.Serialize(
            new { queries },
            new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

    private sealed record QueryResponseItem(
        string Query,
        string Category,
        int Priority);

    private sealed class StubAiSearchQueryClient(string response) : IAiSearchQueryClient
    {
        public AiSearchQueryPrompt? CapturedPrompt { get; private set; }

        public Task<string> GenerateAsync(
            AiSearchQueryPrompt prompt,
            CancellationToken cancellationToken = default)
        {
            CapturedPrompt = prompt;
            return Task.FromResult(response);
        }
    }
}
