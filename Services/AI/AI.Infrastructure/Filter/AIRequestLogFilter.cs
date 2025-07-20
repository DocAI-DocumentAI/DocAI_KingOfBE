using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using AI.Domain.Enums;
using AI.Domain.Models;
using Auth.Infrastructure.Filter;


namespace AI.Infrastructure.FIlter
{
    public class AIRequestLogFilter : IFilter<AIRequestLog>
    {
        public string UserId { get; set; }
        public string RequestId { get; set; }
        public ModelType? ModelType { get; set; }
        public RequestStatus? Status { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }

        public Expression<Func<AIRequestLog, bool>> ToExpression()
        {
            return log =>
                (string.IsNullOrEmpty(UserId) || log.UserId == UserId) &&
                (string.IsNullOrEmpty(RequestId) || log.RequestId.Contains(RequestId)) &&
                (!ModelType.HasValue || log.ModelType == ModelType.Value) &&
                (!Status.HasValue || log.Status == Status.Value) &&
                (!FromDate.HasValue || log.CreatedAt >= FromDate.Value) &&
                (!ToDate.HasValue || log.CreatedAt <= ToDate.Value);
        }
    }
}
