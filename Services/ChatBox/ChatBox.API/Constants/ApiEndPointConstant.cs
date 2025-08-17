namespace ChatBox.API.Constants
{
    public static class ApiEndPointConstant
    {
        static ApiEndPointConstant() { }
        public const string RootEndPoint = "/api";
        public const string ApiVersion = "/chatbox";
        public const string ApiEndpoint = RootEndPoint + ApiVersion;

        public static class Chat
        {
            public const string SendMessage = "send";
            public const string SendMessageStream = "send/stream";
            public const string CreateSession = "session";
            public const string GetSession = "session/{sessionId}";
            public const string GetUserSessions = "sessions";
            public const string DeleteSession = "session/{sessionId}";
            public const string SwitchModel = "session/{sessionId}/model";
            public const string UpdateSessionTitle = "session/{sessionId}/title";
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

            public const string SetActiveModel = "configuration/{configId}/activate";
            public const string DeactivateModel = "configuration/{configId}/deactivate";
            public const string SetDefaultModel = "configuration/{configId}/set-default";
            public const string TestModel = "configuration/{configId}/test";

            public const string Statistics = "statistics";
            public const string DailyActivity = "statistics/daily";
            public const string ModelUsage = "statistics/models";
            public const string ModelImpact = "statistics/impact";
            public const string Bulk = "bulk";
        }

        public static class Preference
        {
            public const string UpdateUserPreferences = "user";
            public const string DeleteUserPreferences = "user";
            public const string GetSessionPreferences = "session/{sessionId}/preferences";
            public const string UpdateSessionPreferences = "session/{sessionId}/preferences";
            public const string DeleteSessionPreferences = "session/{sessionId}/preferences";
            public const string GetUserPreferences = "user/preferences";
            public const string GetAvailableCharacteristics = "preferences/characteristics";
        }
    }
}
