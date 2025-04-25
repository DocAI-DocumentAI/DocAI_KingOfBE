using System.Linq.Expressions;

namespace Auth.API.Filter;

public interface IFilter<T>
{
    Expression<Func<T, bool>> ToExpression();
}