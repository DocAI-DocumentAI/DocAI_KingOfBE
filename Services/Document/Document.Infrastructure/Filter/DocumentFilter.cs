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
        public bool? IsPublic { get; set; }
        //public string? DepartmentId { get; set; }

        public Expression<Func<DocumentVersion, bool>> ToExpression()
        {
            return documentVersion => // Renamed for clarity
            (!Status.HasValue || documentVersion.Status == Status.Value) &&
            (!CreatedTime.HasValue || documentVersion.CreatedTime.Date == CreatedTime.Value.Date) &&
            (!IsPublic.HasValue || documentVersion.IsPublic == IsPublic.Value);
            //&&(!DepartmentId.is || documentVersion.DocumentFile.DepartmentId == DepartmentId.Value);
        }
    }
}
