namespace MarketPulse.Application.Services.SearchQueryGenerator;

public sealed class InvalidAiSearchQueryResponseException : Exception
{
    public InvalidAiSearchQueryResponseException(string message)
        : base(message)
    {
    }

    public InvalidAiSearchQueryResponseException(
        string message,
        Exception innerException)
        : base(message, innerException)
    {
    }
}
