namespace Document.API.Payload.Response
{
    public class DocumentFileResponse
    {
        public string Title { get; set; }

        public string Description { get; set; }

        public string DocumentName { get; set; }

        public string StoragePath { get; set; }

        public DateTime? EffectiveFrom { get; set; }

        public DateTime? EffectiveUntil { get; set; }

        public string Status { get; set; }
    }
}
