using System.ComponentModel.DataAnnotations;

namespace AI.API.Payload.Request
{
    public class BatchEmbeddingRequest
    {
        public string SourceService { get; set; }
        public List<EmbeddingRequest> Items { get; set; }
    }
}
