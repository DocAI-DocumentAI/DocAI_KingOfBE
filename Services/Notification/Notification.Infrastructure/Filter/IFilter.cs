using System;
using System.Linq.Expressions;

namespace Notification.Infrastructure.Filter;

public interface IFilter<T>
{
    Expression<Func<T, bool>> ToExpression();
}