using MarketPulse.Application.Services.Analyser;
using MarketPulse.Application.Services.AnalysisRequest;
using MarketPulse.Application.Services.DataCollection;
using MarketPulse.Application.Services.SearchQueryGenerator;
using MarketPulse.Application.Workers;
using Microsoft.Extensions.DependencyInjection;

namespace MarketPulse.Application
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddApplication(
        this IServiceCollection services)
        {
            services.AddScoped<IAnalysisGenerator, AnalysisGenerator>();
            services.AddScoped<IAnalysisRequestService, AnalysisRequestService>();
            services.AddScoped<ISearchQueryGenerator, AiQueryGenerator>();
            services.AddScoped<ISearchQueryGenerationService, SearchQueryGenerationService>();
            services.AddScoped<IDataCollectorFactory, DataCollectorFactory>();
            services.AddScoped<IDataCollectionOrchestrator, DataCollectionOrchestrator>();

            services.AddHostedService<AnalysisWorker>();

            return services;
        }
    }
}
