using System.ComponentModel.DataAnnotations;

namespace AI.API.Payload.Request
{
    public class BatchEmbeddingRequest
    {
        public List<EmbeddingItem> Items { get; set; } = new();
        public string SourceService { get; set; }
    }
    public class EmbeddingItem
    {
        public string Text { get; set; }
        public string? DocumentId { get; set; }
        public string? VersionId { get; set; }
        public string? Title { get; set; }
        public string? Summary { get; set; }
        public string? TypeName { get; set; }
        public int? DepartmentId { get; set; }
    }
}
