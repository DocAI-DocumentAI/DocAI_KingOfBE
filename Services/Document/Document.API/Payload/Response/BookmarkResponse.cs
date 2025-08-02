namespace Document.API.Payload.Response
{
    public class BookmarkResponse
    {
        public string Id { get; set; }
        public string DocumentId { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string OwnerId { get; set; }
        public string? OwnerName { get; set; }
        public DateTime CreatedTime { get; set; }
    }
}