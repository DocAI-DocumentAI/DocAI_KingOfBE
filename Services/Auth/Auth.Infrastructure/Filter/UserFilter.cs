using System.Linq.Expressions;
using Auth.Domain.Models;

namespace Auth.Infrastructure.Filter;

public class UserFilter : IFilter<User>
{
    public string? Email { get; set; }
    public string? FullName { get; set; }
    public string? Phone { get; set; }
    public Guid? RoleId { get; set; }
    public Guid? DepartmentId { get; set; }

    public Expression<Func<User, bool>> ToExpression()
    {
        return user =>
            (string.IsNullOrEmpty(Email) || user.Email.Contains(Email)) &&
            (string.IsNullOrEmpty(FullName) || user.FullName.Contains(FullName)) &&
            (string.IsNullOrEmpty(Phone) || user.Phone.Contains(Phone)) &&
            (!RoleId.HasValue || user.RoleId == RoleId) &&
            (!DepartmentId.HasValue || user.DepartmentId == DepartmentId);
    }
}