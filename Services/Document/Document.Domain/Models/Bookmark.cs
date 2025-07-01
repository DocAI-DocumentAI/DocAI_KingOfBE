using Document.Domain.Models;

namespace Document.Domain.Model
{
    public class Bookmark : BaseEntity
    {
        public string UserId { get; set; }
        public string DocumentVersionId { get; set; }
        public DocumentVersion DocumentVersion { get; set; }
    }
}