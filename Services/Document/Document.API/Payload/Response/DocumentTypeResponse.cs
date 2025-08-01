namespace Document.API.Payload.Response
{
    public class DocumentTypeResponse
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string? Description { get; set; }
        public string CreatedBy { get; set; }
        public string? LastUpdatedBy { get; set; }
        public DateTime CreatedTime { get; set; }
        public DateTime? LastUpdatedTime { get; set; }
        public int DocumentCount { get; set; }
    }
}
