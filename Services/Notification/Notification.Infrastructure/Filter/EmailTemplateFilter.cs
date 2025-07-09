using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Notification.Domain.Models;

namespace Notification.Infrastructure.Filter
{
    public class EmailTemplateFilter : IFilter<EmailTemplate>
    {
        public string? TemplateName { get; set; }
        public string? Subject { get; set; }
        public string? AssociatedEvent { get; set; }

        public Expression<Func<EmailTemplate, bool>> ToExpression()
        {
            return template =>
                (string.IsNullOrEmpty(TemplateName) || template.TemplateName.Contains(TemplateName)) &&
                (string.IsNullOrEmpty(Subject) || template.Subject.Contains(Subject)) &&
                (string.IsNullOrEmpty(AssociatedEvent) || (template.AssociatedEvent != null && template.AssociatedEvent.Contains(AssociatedEvent)));
        }
    }
}