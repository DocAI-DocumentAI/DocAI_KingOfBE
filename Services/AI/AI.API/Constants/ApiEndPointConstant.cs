namespace AI.API.Constants;

    public static class ApiEndPointConstant
    {
        public const string API_VERSION = "v1";
        public const string API_PREFIX = "api/" + API_VERSION;

        // Route prefixes
        public const string ADMIN_PREFIX = API_PREFIX + "/admin";
        public const string PUBLIC_PREFIX = API_PREFIX + "/public";

        // Common routes
        public const string HEALTH_ROUTE = API_PREFIX + "/health";
        public const string INFO_ROUTE = API_PREFIX + "/info";
    }
