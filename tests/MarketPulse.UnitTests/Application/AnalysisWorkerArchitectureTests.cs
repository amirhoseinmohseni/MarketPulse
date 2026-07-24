using MarketPulse.Application.Services.Analyser;
using MarketPulse.Application.Workers;

namespace MarketPulse.UnitTests.Application;

public class AnalysisWorkerArchitectureTests
{
    [Fact]
    public void Worker_DoesNotDependDirectlyOnAiClientOrAnalysisGenerator()
    {
        var dependencyTypes = typeof(AnalysisWorker)
            .GetConstructors()
            .Single()
            .GetParameters()
            .Select(parameter => parameter.ParameterType)
            .Concat(
                typeof(AnalysisWorker)
                    .GetFields(
                        System.Reflection.BindingFlags.Instance
                        | System.Reflection.BindingFlags.NonPublic)
                    .Select(field => field.FieldType))
            .ToList();

        Assert.DoesNotContain(typeof(IAiMarketInsightClient), dependencyTypes);
        Assert.DoesNotContain(typeof(IAnalysisGenerator), dependencyTypes);
    }
}
