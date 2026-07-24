using MarketPulse.Application.Services.Analyser;
using MarketPulse.Application.Services.AnalysisRequest;
using MarketPulse.Application.Services.DataCollection;
using MarketPulse.Application.Services.SearchQueryGenerator;
using MarketPulse.Application.Workers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MarketPulse.Application
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddApplication(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            services.AddSingleton(CreateMarketInsightAnalysisOptions(configuration));
            services.AddScoped<IMarketInsightInputBuilder, MarketInsightInputBuilder>();
            services.AddScoped<IMarketInsightSignalEvaluator, MarketInsightSignalEvaluator>();
            services.AddScoped<IMarketInsightPromptBuilder, MarketInsightPromptBuilder>();
            services.AddScoped<IMarketInsightResponseParser, MarketInsightResponseParser>();
            services.AddScoped<IAnalysisGenerator, AnalysisGenerator>();
            services.AddScoped<IAnalysisRequestService, AnalysisRequestService>();
            services.AddScoped<ISearchQueryGenerator, AiQueryGenerator>();
            services.AddScoped<ISearchQueryGenerationService, SearchQueryGenerationService>();
            services.AddScoped<IDataCollectorFactory, DataCollectorFactory>();
            services.AddScoped<IDataCollectionOrchestrator, DataCollectionOrchestrator>();

            services.AddHostedService<AnalysisWorker>();

            return services;
        }

        private static MarketInsightAnalysisOptions CreateMarketInsightAnalysisOptions(
            IConfiguration configuration)
        {
            var defaults = new MarketInsightAnalysisOptions();
            var section = configuration.GetSection(MarketInsightAnalysisOptions.SectionName);

            var moderateMinItems = PositiveInt(section["ModerateMinItems"], defaults.ModerateMinItems);
            var moderateMinSources = PositiveInt(section["ModerateMinSources"], defaults.ModerateMinSources);

            return new MarketInsightAnalysisOptions
            {
                MaxItems = PositiveInt(section["MaxItems"], defaults.MaxItems),
                MaxIdeaLength = PositiveInt(section["MaxIdeaLength"], defaults.MaxIdeaLength),
                MaxSourceLength = PositiveInt(section["MaxSourceLength"], defaults.MaxSourceLength),
                MaxTitleLength = PositiveInt(section["MaxTitleLength"], defaults.MaxTitleLength),
                MaxContentLengthPerItem = PositiveInt(
                    section["MaxContentLengthPerItem"],
                    defaults.MaxContentLengthPerItem),
                MaxTotalItemCharacters = PositiveInt(
                    section["MaxTotalItemCharacters"],
                    defaults.MaxTotalItemCharacters),
                MinUsableTextLength = PositiveInt(
                    section["MinUsableTextLength"],
                    defaults.MinUsableTextLength),
                ModerateMinItems = moderateMinItems,
                ModerateMinSources = moderateMinSources,
                StrongMinItems = Math.Max(
                    moderateMinItems,
                    PositiveInt(section["StrongMinItems"], defaults.StrongMinItems)),
                StrongMinSources = Math.Max(
                    moderateMinSources,
                    PositiveInt(section["StrongMinSources"], defaults.StrongMinSources)),
                MinInsightCount = PositiveInt(section["MinInsightCount"], defaults.MinInsightCount),
                MaxInsightsPerCategory = PositiveInt(
                    section["MaxInsightsPerCategory"],
                    defaults.MaxInsightsPerCategory),
                MaxEvidencePerInsight = PositiveInt(
                    section["MaxEvidencePerInsight"],
                    defaults.MaxEvidencePerInsight),
                MaxSummaryLength = PositiveInt(
                    section["MaxSummaryLength"],
                    defaults.MaxSummaryLength),
                MaxInsightTextLength = PositiveInt(
                    section["MaxInsightTextLength"],
                    defaults.MaxInsightTextLength)
            };
        }

        private static int PositiveInt(string? value, int fallback)
            => int.TryParse(value, out var parsed) && parsed > 0
                ? parsed
                : fallback;
    }
}
