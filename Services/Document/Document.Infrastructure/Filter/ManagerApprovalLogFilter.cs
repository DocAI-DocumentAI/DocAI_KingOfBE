using Document.Domain.Enums;
using Document.Domain.Models;
using System;
using System.Linq.Expressions;

namespace Document.Infrastructure.Filter
{
    public class ManagerApprovalLogFilter : IFilter<ApprovalLog>
    {
        public ApprovalAction? Action { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public string? SubmittedBy { get; set; }
        public string? DocumentTitle { get; set; }

        public Expression<Func<ApprovalLog, bool>> ToExpression()
        {
            return log =>
                (!Action.HasValue || log.Action == Action.Value) &&
                (!FromDate.HasValue || log.CreatedTime >= FromDate.Value) &&
                (!ToDate.HasValue || log.CreatedTime <= ToDate.Value) &&
                (string.IsNullOrEmpty(SubmittedBy) || log.DocumentVersion.SubmittedBy == SubmittedBy) &&
                (string.IsNullOrEmpty(DocumentTitle) || log.DocumentVersion.DocumentFile.Title.ToLower().Contains(DocumentTitle.ToLower()));
        }
    }
}

