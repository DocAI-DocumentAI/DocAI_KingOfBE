namespace Notification.API.Constants
{
    public static class ApiConstants
    {
        public const string DEFAULT_CONFIG_KEY = "Default";
        public const string DOCUMENT_NEARING_EXPIRATION_TEMPLATE = "DocumentNearingExpiration";
        public const string DOCUMENT_EXPIRED_TEMPLATE = "DocumentExpired";
        public const int DEFAULT_PAGE_SIZE = 10;
        public const int MAX_PAGE_SIZE = 100;
        public const int EMAIL_TIMEOUT_SECONDS = 30;
        public const int CACHE_DURATION_MINUTES = 5;
    }
}
