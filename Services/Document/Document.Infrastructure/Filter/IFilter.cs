using System;
using System.Linq.Expressions;

namespace Document.Infrastructure.Filter;

public interface IFilter<T>
{
    Expression<Func<T, bool>> ToExpression();
}