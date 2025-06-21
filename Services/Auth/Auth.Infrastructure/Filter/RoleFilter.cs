using System.Linq.Expressions;
using Auth.Domain.Models;
namespace Auth.Infrastructure.Filter;

public class RoleFilter : IFilter<Role>
{
    public string? Description { get; set; }
    public string? RoleName { get; set; }

    public Expression<Func<Role, bool>> ToExpression()
    {
        return role =>
            (string.IsNullOrEmpty(RoleName) || role.RoleName.Contains(RoleName)) &&
            (string.IsNullOrEmpty(Description) || role.Description.Contains(Description));           
    }
}