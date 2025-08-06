namespace ChatBox.API.Payload.Response
{
    public class ModelImpactResponse
    {
        public string ModelName { get; set; }
        public int ActiveSessionsCount { get; set; }
        public int AffectedUsersCount { get; set; }
        public DateTime LastUsed { get; set; }
        public bool CanSafelyDeactivate { get; set; }
        public string Impact { get; set; }
        public List<string> Recommendations { get; set; } = new();
    }
}
