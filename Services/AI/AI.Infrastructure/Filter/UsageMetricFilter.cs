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
    public class UsageMetricFilter : IFilter<UsageMetric>
    {
        public string? UserId { get; set; }
        public ModelType? ModelType { get; set; }
        public RequestStatus? Status { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }

        public Expression<Func<UsageMetric, bool>> ToExpression()
        {
            return metric =>
                (string.IsNullOrEmpty(UserId) || metric.UserId == UserId) &&
                (!ModelType.HasValue || metric.ModelType == ModelType.Value) &&
                (!Status.HasValue || metric.Status == Status.Value) &&
                (!FromDate.HasValue || metric.CreatedAt >= FromDate.Value) &&
                (!ToDate.HasValue || metric.CreatedAt <= ToDate.Value);
        }
    }
}