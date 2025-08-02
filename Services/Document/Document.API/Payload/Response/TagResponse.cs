namespace Document.API.Payload.Response
{
    public class TagResponse
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string CreatedBy { get; set; }
        public string? CreatedByName { get; set; }
        public DateTime CreatedTime { get; set; }
        public string? LastUpdatedBy { get; set; }
        public string? LastUpdatedByName { get; set; }
        public DateTime? LastUpdatedTime { get; set; }
    }
}
