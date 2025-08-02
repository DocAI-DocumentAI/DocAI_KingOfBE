using FluentValidation;
using Auth.API.Payload.Request.Role;

namespace Auth.API.Validators.Role;

public class UpdateRoleRequestValidator : AbstractValidator<UpdateRoleRequest>
{
    public UpdateRoleRequestValidator()
    {
        RuleFor(x => x.RoleName)
            .Length(2, 50).WithMessage("Tên vai trò phải từ 2-50 ký tự")
            .Matches(@"^[a-zA-ZÀ-ỹ0-9\s\-_]+$").WithMessage("Tên vai trò chỉ được chứa chữ cái, số, khoảng trắng, dấu gạch ngang và gạch dưới")
            .When(x => !string.IsNullOrEmpty(x.RoleName));

        RuleFor(x => x.Description)
            .MaximumLength(300).WithMessage("Mô tả không được vượt quá 300 ký tự")
            .When(x => !string.IsNullOrEmpty(x.Description));
    }
}