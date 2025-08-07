using Document.API.Payload.Response;

namespace Document.API.Services.Interfaces;

/// <summary>
/// Service for logging and tracking AI token usage for cost monitoring and performance analysis
/// </summary>
public interface ITokenUsageLogger
{
    /// <summary>
    /// Log token usage for an AI service call
    /// </summary>
    /// <param name="operation">The operation being performed (e.g., "DocumentAnalysis", "SummaryGeneration")</param>
    /// <param name="requestTokens">Number of tokens in the request/prompt</param>
    /// <param name="responseTokens">Number of tokens in the response</param>
    /// <param name="modelUsed">The AI model used for the operation</param>
    /// <param name="userId">ID of the user who initiated the operation</param>
    /// <param name="documentId">ID of the document being processed (if applicable)</param>
    /// <param name="processingTimeMs">Time taken to process the request in milliseconds</param>
    /// <param name="success">Whether the operation was successful</param>
    /// <param name="errorMessage">Error message if the operation failed</param>
    Task LogTokenUsageAsync(
        string operation,
        int requestTokens,
        int responseTokens,
        string? modelUsed = null,
        string? userId = null,
        string? documentId = null,
        long? processingTimeMs = null,
        bool success = true,
        string? errorMessage = null);

    /// <summary>
    /// Create a TokenUsageInfo object for response models
    /// </summary>
    /// <param name="requestTokens">Number of tokens in the request</param>
    /// <param name="responseTokens">Number of tokens in the response</param>
    /// <param name="modelUsed">The AI model used</param>
    /// <param name="estimatedCost">Estimated cost for the operation</param>
    /// <returns>TokenUsageInfo object</returns>
    TokenUsageInfo CreateTokenUsageInfo(
        int requestTokens,
        int responseTokens,
        string? modelUsed = null,
        decimal? estimatedCost = null);

    /// <summary>
    /// Estimate token count for a given text
    /// </summary>
    /// <param name="text">Text to count tokens for</param>
    /// <param name="modelName">Model name for accurate token counting</param>
    /// <returns>Estimated token count</returns>
    int EstimateTokenCount(string text, string? modelName = null);

    /// <summary>
    /// Get token usage statistics for a specific time period
    /// </summary>
    /// <param name="startDate">Start date for the statistics</param>
    /// <param name="endDate">End date for the statistics</param>
    /// <param name="userId">Optional user ID to filter statistics</param>
    /// <returns>Token usage statistics</returns>
    Task<TokenUsageStatistics> GetTokenUsageStatisticsAsync(
        DateTime startDate,
        DateTime endDate,
        string? userId = null);
}

/// <summary>
/// Statistics for token usage over a time period
/// </summary>
public class TokenUsageStatistics
{
    public int TotalRequests { get; set; }
    public long TotalRequestTokens { get; set; }
    public long TotalResponseTokens { get; set; }
    public long TotalTokens => TotalRequestTokens + TotalResponseTokens;
    public decimal TotalEstimatedCost { get; set; }
    public Dictionary<string, int> OperationBreakdown { get; set; } = new();
    public Dictionary<string, long> ModelUsageBreakdown { get; set; } = new();
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public double AverageTokensPerRequest => TotalRequests > 0 ? (double)TotalTokens / TotalRequests : 0;
    public double AverageProcessingTimeMs { get; set; }
    public int SuccessfulRequests { get; set; }
    public int FailedRequests { get; set; }
    public double SuccessRate => TotalRequests > 0 ? (double)SuccessfulRequests / TotalRequests * 100 : 0;
}
