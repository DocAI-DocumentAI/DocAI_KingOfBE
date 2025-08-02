using FluentValidation;
using Auth.API.Payload.Request.Role;
using Auth.API.Services.Interface;

namespace Auth.API.Validators.Role;

public class CreateRoleRequestValidator : AbstractValidator<CreateRoleRequest>
{
    private readonly IValidationService _validationService;

    public CreateRoleRequestValidator(IValidationService validationService)
    {
        _validationService = validationService;

        RuleFor(x => x.RoleName)
            .NotEmpty().WithMessage("Tên vai trò không được để trống")
            .Length(2, 50).WithMessage("Tên vai trò phải từ 2-50 ký tự")
            .Matches(@"^[a-zA-ZÀ-ỹ0-9\s\-_]+$").WithMessage("Tên vai trò chỉ được chứa chữ cái, số, khoảng trắng, dấu gạch ngang và gạch dưới")
            .MustAsync(async (name, cancellation) => !await _validationService.IsRoleNameExistsAsync(name))
            .WithMessage("Tên vai trò đã tồn tại");

        RuleFor(x => x.Description)
            .MaximumLength(300).WithMessage("Mô tả không được vượt quá 300 ký tự");
    }
}
