using MarketPulse.Application;
using MarketPulse.Application.Services.SearchQueryGenerator;
using MarketPulse.Domain.Repositories;
using MarketPulse.Infrastructure.AI;
using MarketPulse.Infrastructure.Persistence;
using MarketPulse.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MarketPulse.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            var connectionString = configuration.GetConnectionString("DefaultConnection");

            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseNpgsql(connectionString));

            services.AddScoped<IAnalysisRequestRepository, AnalysisRequestRepository>();
            services.AddScoped<IAnalysisResultRepository, AnalysisResultRepository>();
            services.AddScoped<ISearchQueryRepository, SearchQueryRepository>();
            services.AddSingleton<IBackgroundTaskQueue, BackgroundTaskQueue>();
            services.AddSingleton(CreateOpenRouterOptions(configuration));
            services.AddHttpClient<IAiSearchQueryClient, OpenRouterAiSearchQueryClient>();

            return services;
        }

        private static OpenRouterOptions CreateOpenRouterOptions(IConfiguration configuration)
        {
            var section = configuration.GetSection(OpenRouterOptions.SectionName);

            return new OpenRouterOptions
            {
                ApiKey = section["ApiKey"] ?? string.Empty,
                Model = section["Model"] ?? "openrouter/auto",
                Endpoint = section["Endpoint"] ?? "https://openrouter.ai/api/v1/chat/completions",
                Temperature = double.TryParse(section["Temperature"], out var temperature)
                    ? temperature
                    : 0.2,
                MaxTokens = int.TryParse(section["MaxTokens"], out var maxTokens)
                    ? maxTokens
                    : 800
            };
        }
    }
}
