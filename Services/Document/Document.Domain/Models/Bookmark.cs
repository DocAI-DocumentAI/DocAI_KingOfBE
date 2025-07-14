using Document.Domain.Models;

namespace Document.Domain.Model
{
    public class Bookmark : BaseEntity
    {
        public string UserId { get; set; }
        public string DocumentId { get; set; }
        public DocumentFile Document { get; set; }
    }
}