using System.Linq.Expressions;
using Auth.Domain.Models;

namespace Auth.Infrastructure.Filter;

public class DepartmentFilter : IFilter<Department>
{
    public string? Description { get; set; }
    public string? DepartmentName { get; set; }

    public Expression<Func<Department, bool>> ToExpression()
    {
        return Department =>
            (string.IsNullOrEmpty(DepartmentName) || Department.Name.Contains(DepartmentName)) &&
            (string.IsNullOrEmpty(Description) || Department.Description.Contains(Description));           
    }
}