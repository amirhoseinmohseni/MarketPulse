using MarketPulse.Domain.Entities;
using MarketPulse.Domain.Enums;
using MarketPulse.Domain.Repositories;

namespace MarketPulse.Application.Services.Analyser
{
    public sealed class AnalysisGenerator : IAnalysisGenerator
    {
        private readonly ICollectedMarketItemRepository _collectedMarketItemRepository;
        private readonly IAiMarketInsightClient _aiClient;
        private readonly IMarketInsightInputBuilder _inputBuilder;
        private readonly IMarketInsightSignalEvaluator _signalEvaluator;
        private readonly IMarketInsightPromptBuilder _promptBuilder;
        private readonly IMarketInsightResponseParser _responseParser;

        public AnalysisGenerator(
            ICollectedMarketItemRepository collectedMarketItemRepository,
            IAiMarketInsightClient aiClient,
            IMarketInsightInputBuilder inputBuilder,
            IMarketInsightSignalEvaluator signalEvaluator,
            IMarketInsightPromptBuilder promptBuilder,
            IMarketInsightResponseParser responseParser)
        {
            _collectedMarketItemRepository = collectedMarketItemRepository;
            _aiClient = aiClient;
            _inputBuilder = inputBuilder;
            _signalEvaluator = signalEvaluator;
            _promptBuilder = promptBuilder;
            _responseParser = responseParser;
        }

        public async Task<AnalysisResult> AnalyseRequest(Guid requestId, string idea, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            var collectedItems = await _collectedMarketItemRepository
                .GetByAnalysisRequestIdAsync(requestId, ct);
            ct.ThrowIfCancellationRequested();

            var buildResult = _inputBuilder.Build(requestId, idea, collectedItems);
            var signalAssessment = _signalEvaluator.Evaluate(buildResult.Input.Items);

            if (buildResult.Input.Items.Count == 0)
            {
                return CreateNoDataResult(requestId);
            }

            var prompt = _promptBuilder.Build(
                buildResult.Input,
                signalAssessment.MaximumSignalStrength);
            var rawResponse = await _aiClient.GenerateAsync(prompt, ct);
            ct.ThrowIfCancellationRequested();

            var response = _responseParser.ParseAndValidate(
                rawResponse,
                buildResult.EvidenceMap,
                signalAssessment.MaximumSignalStrength);

            var summary = signalAssessment.MaximumSignalStrength == SignalStrength.Weak
                ? $"{signalAssessment.WeakSignalSummary} {response.Summary}".Trim()
                : response.Summary;

            var resultId = Guid.NewGuid();
            var result = new AnalysisResult
            {
                Id = resultId,
                AnalysisRequestId = requestId,
                MarketScore = response.MarketScore,
                SignalStrength = response.SignalStrength,
                Summary = summary
            };

            AddInsights(result, response.Strengths, InsightType.Strength);
            AddInsights(result, response.Weaknesses, InsightType.Weakness);
            AddInsights(result, response.Opportunities, InsightType.Opportunity);
            AddInsights(result, response.Risks, InsightType.Risk);

            return result;
        }

        private static AnalysisResult CreateNoDataResult(Guid requestId)
            => new()
            {
                Id = Guid.NewGuid(),
                AnalysisRequestId = requestId,
                MarketScore = null,
                SignalStrength = SignalStrength.Weak,
                Summary = "No usable collected market data was available. The market signal is weak and no score can be supported."
            };

        private static void AddInsights(
            AnalysisResult result,
            IReadOnlyList<ValidatedMarketInsight> insights,
            InsightType type)
        {
            for (var position = 0; position < insights.Count; position++)
            {
                var source = insights[position];
                var insight = new AnalysisInsight
                {
                    Id = Guid.NewGuid(),
                    AnalysisResultId = result.Id,
                    Type = type,
                    Position = position,
                    Text = source.Text
                };

                foreach (var collectedMarketItemId in source.CollectedMarketItemIds)
                {
                    insight.Evidence.Add(new AnalysisEvidence
                    {
                        Id = Guid.NewGuid(),
                        AnalysisInsightId = insight.Id,
                        CollectedMarketItemId = collectedMarketItemId
                    });
                }

                result.Insights.Add(insight);
            }
        }
    }
}
