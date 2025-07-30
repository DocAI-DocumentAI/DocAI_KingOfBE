namespace ChatBox.API.Constants
{
    public class ChatServiceConstants
    {
        public const string ServiceUserId = "chatbox-service-user";
        public const string DefaultAIModel = "gpt-3.5-turbo";
        public const double DefaultTemperature = 0.7;
        public const int DefaultMaxTokens = 2000;
        public const int MaxMessageLength = 5000;
        public const int MaxInputTokens = 3000;
        public const int MaxContextLength = 8000;
        public const int MaxHistoryTokens = 1000;
        public const int MaxSessionTitleLength = 50;
        public const int DefaultSummaryLength = 200;
        public const int ReservedTokensForResponse = 500;
        public const double EstimatedCharsPerToken = 3.5;
        public const double SecurityRiskThreshold = 0.7;
        public const double IntentClarificationThreshold = 0.7;
        public const int StreamingChunkSize = 50;
        public const int StreamingDelayMs = 100;
        public const int SearchContextSize = 100;
        public const int DocSearchLimit = 5;
        public const double DocMinRelevance = 0.7;
        public const int ContextWindowSize = 10;
        public const int RequestTimeoutSeconds = 120;
        public const int RetryAttempts = 3;
        public const int RetryDelayMs = 1000;
    }
}
