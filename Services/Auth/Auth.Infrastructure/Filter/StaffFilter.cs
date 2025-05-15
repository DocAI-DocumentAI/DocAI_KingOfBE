using System.Linq.Expressions;
using Auth.API.Models;

namespace Auth.API.Filter;

public class StaffFilter : IFilter<Staff>
{
    public string? Email { get; set; }
    public string? Username { get; set; }
    public string? Phone { get; set; }
    public string? FullName { get; set; }
    public string? Type { get; set; }
    public DateTime? CreateAt { get; set; }
    public DateTime? UpdateAt { get; set; }
    public bool? TwoFactorEnabled { get; set; }
    public string? TwoFactorMethod { get; set; }

    public Expression<Func<Staff, bool>> ToExpression()
    {
        return member =>
            (string.IsNullOrEmpty(Email) || member.User.Email.Contains(Email)) &&
            (string.IsNullOrEmpty(Username) || member.User.UserName.Contains(Username)) &&
            (string.IsNullOrEmpty(Phone) || member.User.Phone.Contains(Phone)) &&
            (string.IsNullOrEmpty(FullName) || member.User.FullName.Contains(FullName)) &&
            (string.IsNullOrEmpty(Type) || member.Type.Contains(Type)) &&
            (!CreateAt.HasValue || member.CreateAt.Date == CreateAt.Value.Date) &&
            (!UpdateAt.HasValue || member.UpdateAt.Date == UpdateAt.Value.Date) &&
            (!TwoFactorEnabled.HasValue || member.User.TwoFactorEnabled == TwoFactorEnabled.Value) &&
            (string.IsNullOrEmpty(TwoFactorMethod) || member.User.TwoFactorMethod.Contains(TwoFactorMethod));          
    }
}