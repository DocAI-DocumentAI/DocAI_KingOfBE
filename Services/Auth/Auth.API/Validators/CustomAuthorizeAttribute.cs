using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.ComponentModel;
using System;
using System.Linq;
using System.Threading.Tasks;
using Auth.API.Utils;
using Auth.Domain.Enums;
using Microsoft.AspNetCore.Authorization;

// Định nghĩa các loại Permission cần kiểm tra
public class CustomAuthorizeAttribute : AuthorizeAttribute
{
    public CustomAuthorizeAttribute(params RoleEnum[] roleEnums)
    {
        var allowedRoleAsString = roleEnums.Select(x => x.GetDescriptionFromEnum());
        Roles = string.Join(",", allowedRoleAsString);
    }
}
