using Document.Domain.Enums;
using Document.Domain.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Document.Infrastructure.Filter
{
    public class DocumentFilter : IFilter<DocumentVersion>
    {
        public StatusEnum? Status { get; set; }
        public DateTime? CreatedTime { get; set; }

        public Expression<Func<DocumentVersion, bool>> ToExpression()
        {
            return DocumentVersion =>
                (!Status.HasValue || DocumentVersion.Status == Status.Value) &&
                (!CreatedTime.HasValue || DocumentVersion.CreatedTime.Date == CreatedTime.Value.Date);
        }
    }
}
