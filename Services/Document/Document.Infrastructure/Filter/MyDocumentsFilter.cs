using Document.Domain.Enums;
using Document.Domain.Models;
using System.Linq.Expressions;

namespace Document.Infrastructure.Filter
{
    public class MyDocumentsFilter : IFilter<DocumentVersion>
    {
        public string? Title { get; set; }
        //public string? DepartmentId { get; set; }
        public bool? IsPublic { get; set; }
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }
        public StatusEnum? Status { get; set; }
        public string? DocumentTypeId { get; set; }

        public Expression<Func<DocumentVersion, bool>> ToExpression()
        {
            return doc =>
       (string.IsNullOrEmpty(Title) || doc.Title.Contains(Title)) &&
       //(string.IsNullOrEmpty(DepartmentId) || doc.DocumentFile.DepartmentId == DepartmentId) &&
       (!IsPublic.HasValue || doc.IsPublic == IsPublic.Value) &&
       (!From.HasValue || doc.CreatedTime >= From.Value) &&
       (!To.HasValue || doc.CreatedTime <= To.Value) &&
       (!Status.HasValue || doc.Status == Status.Value) &&
       (string.IsNullOrEmpty(DocumentTypeId) || doc.DocumentFile.DocumentTypeId == DocumentTypeId);
        }
    }
}
