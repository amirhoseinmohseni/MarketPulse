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
            services.AddSingleton<IBackgroundTaskQueue, BackgroundTaskQueue>();
            services.AddSingleton(CreateOpenAiOptions(configuration));
            services.AddSingleton<HttpClient>();
            services.AddScoped<IAiSearchQueryClient, OpenAiSearchQueryClient>();

            return services;
        }

        private static OpenAiOptions CreateOpenAiOptions(IConfiguration configuration)
        {
            var section = configuration.GetSection(OpenAiOptions.SectionName);

            return new OpenAiOptions
            {
                ApiKey = section["ApiKey"] ?? string.Empty,
                Model = section["Model"] ?? "gpt-4o-mini",
                Endpoint = section["Endpoint"] ?? "https://api.openai.com/v1/chat/completions",
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
