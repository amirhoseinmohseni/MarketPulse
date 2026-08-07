namespace MarketPulse.Application.Services.Analyser;

public sealed class InvalidAiMarketInsightResponseException : Exception
{
    public InvalidAiMarketInsightResponseException(string message)
        : base(message)
    {
    }

    public InvalidAiMarketInsightResponseException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
