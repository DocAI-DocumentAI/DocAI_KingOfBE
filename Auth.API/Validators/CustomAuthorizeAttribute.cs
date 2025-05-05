using System.Linq;
using Auth.API.Enums;
using Auth.API.Utils;
using Microsoft.AspNetCore.Authorization;

namespace Auth.API.Validators;

public class CustomAuthorizeAttribute : AuthorizeAttribute
{
    public CustomAuthorizeAttribute(params RoleEnum[] roleEnums)
    {
        var allowedRoleAsString = roleEnums.Select(x => x.GetDescriptionFromEnum());
        Roles = string.Join(",", allowedRoleAsString);
    }
}