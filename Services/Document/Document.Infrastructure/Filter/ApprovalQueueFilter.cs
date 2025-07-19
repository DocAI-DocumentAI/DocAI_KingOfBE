using Document.Domain.Models;
using System.Linq.Expressions;
using Document.Domain.Enums;

namespace Document.Infrastructure.Filter
{
    public class ApprovalQueueFilter : IFilter<DocumentVersion>
    {
        public string? UserId { get; set; }
        public StatusEnum? Status { get; set; }
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }

        public Expression<Func<DocumentVersion, bool>> ToExpression()
        {
            return doc =>
                (string.IsNullOrEmpty(UserId) || doc.SubmittedBy == UserId) &&
                (!Status.HasValue || doc.Status == Status.Value) &&
                (!From.HasValue || doc.LastSubmitted >= From.Value) &&
                (!To.HasValue || doc.LastSubmitted <= To.Value);
        }
    }
}