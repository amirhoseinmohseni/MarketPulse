using MarketPulse.Application;
using MarketPulse.Application.Services.DataCollection;
using MarketPulse.Application.Services.RedditDataCollection;
using MarketPulse.Application.Services.SearchQueryGenerator;
using MarketPulse.Domain.Repositories;
using MarketPulse.Infrastructure.AI;
using MarketPulse.Infrastructure.Persistence;
using MarketPulse.Infrastructure.Reddit;
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
            services.AddScoped<IRedditPostRepository, RedditPostRepository>();
            services.AddScoped<ICollectedMarketItemRepository, CollectedMarketItemRepository>();
            services.AddSingleton<IBackgroundTaskQueue, BackgroundTaskQueue>();
            services.AddSingleton(CreateDataCollectorOptions(configuration));
            services.AddSingleton(CreateOpenRouterOptions(configuration));
            services.AddSingleton(CreateRedditOptions(configuration));
            services.AddHttpClient<IAiSearchQueryClient, OpenRouterAiSearchQueryClient>();
            services.AddHttpClient(RedditAccessTokenProvider.HttpClientName);
            services.AddSingleton<IRedditAccessTokenProvider, RedditAccessTokenProvider>();
            services.AddHttpClient<IRedditClient, RedditClient>();
            services.AddScoped<IDataCollector, RedditDataCollector>();

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

        private static DataCollectorOptions CreateDataCollectorOptions(IConfiguration configuration)
        {
            var section = configuration.GetSection(DataCollectorOptions.SectionName);
            var sourcesSection = section.GetSection("Sources");
            var options = new DataCollectorOptions
            {
                FailFast = bool.TryParse(section["FailFast"], out var failFast) && failFast
            };

            foreach (var sourceSection in sourcesSection.GetChildren())
            {
                options.Sources[sourceSection.Key] = new DataCollectorSourceOptions
                {
                    Enabled = bool.TryParse(sourceSection["Enabled"], out var enabled) && enabled
                };
            }

            return options;
        }

        private static RedditOptions CreateRedditOptions(IConfiguration configuration)
        {
            var section = configuration.GetSection(RedditOptions.SectionName);

            return new RedditOptions
            {
                ClientId = section["ClientId"] ?? string.Empty,
                ClientSecret = section["ClientSecret"] ?? string.Empty,
                UserAgent = section["UserAgent"] ?? "MarketPulse/1.0",
                AuthUrl = section["AuthUrl"] ?? "https://www.reddit.com/api/v1/access_token",
                BaseUrl = section["BaseUrl"] ?? "https://oauth.reddit.com",
                DefaultLimit = int.TryParse(section["DefaultLimit"], out var defaultLimit)
                    ? defaultLimit
                    : 25
            };
        }
    }
}
