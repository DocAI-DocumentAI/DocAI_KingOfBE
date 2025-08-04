namespace ChatBox.API.Constants
{
    public static class ApiEndPointConstant
    {
        public const string ApiEndpoint = "api/v1/[controller]";

        public static class Chat
        {
            public const string SendMessage = "send";
            public const string SendMessageStream = "send/stream";
            public const string CreateSession = "session";
            public const string GetSession = "session/{sessionId}";
            public const string GetUserSessions = "sessions";
            public const string DeleteSession = "session/{sessionId}";
            public const string SwitchModel = "session/{sessionId}/model";
            public const string ValidateMessage = "validate";
            public const string AvailableModels = "models";
            public const string SuggestTitle = "suggest-title";
        }

        public static class Admin
        {
            public const string GetConfigurations = "configurations";
            public const string CreateConfiguration = "configuration";
            public const string UpdateConfiguration = "configuration/{configId}";
            public const string DeleteConfiguration = "configuration/{configId}";
            public const string SetActiveModel = "configuration/{modelName}/activate";
            public const string TestModel = "configuration/{modelName}/test";
            public const string Statistics = "statistics";
            public const string DailyActivity = "statistics/daily";
            public const string ModelUsage = "statistics/models";
        }

        public static class Preference
        {
            public const string GetUserPreferences = "user";
            public const string UpdateUserPreferences = "user";
            public const string GetSessionPreferences = "session/{sessionId}";
            public const string UpdateSessionPreferences = "session/{sessionId}";
            public const string DeleteUserPreferences = "user";
            public const string DeleteSessionPreferences = "session/{sessionId}";
        }
    }
}
