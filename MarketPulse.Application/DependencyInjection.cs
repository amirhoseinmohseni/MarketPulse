using MarketPulse.Application.Services.Analysis;
using MarketPulse.Application.Workers;
using MarketPulse.Domain.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarketPulse.Application
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddApplication(
        this IServiceCollection services)
        {
            services.AddScoped<IAnalyser, FakeAnalysisGenerator>();

            services.AddHostedService<AnalysisWorker>();

            return services;
        }
    }
}
