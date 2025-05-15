using System;
using System.Linq.Expressions;

namespace Auth.Infrastructure.Filter;

public interface IFilter<T>
{
    Expression<Func<T, bool>> ToExpression();
}