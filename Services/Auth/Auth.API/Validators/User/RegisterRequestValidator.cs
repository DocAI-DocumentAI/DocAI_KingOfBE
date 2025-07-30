using FluentValidation;
using Auth.API.Payload.Request.User;
using Auth.API.Services.Interface;
using Auth.API.Payload.Request;

namespace Auth.API.Validators.User;

public class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    private readonly IValidationService _validationService;

    public RegisterRequestValidator(IValidationService validationService)
    {
        _validationService = validationService;

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email không được để trống")
            .EmailAddress().WithMessage("Email không đúng định dạng")
            .MaximumLength(255).WithMessage("Email không được vượt quá 255 ký tự")
            .MustAsync(async (email, cancellation) => !await _validationService.IsEmailExistsAsync(email))
            .WithMessage("Email đã được sử dụng");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Mật khẩu không được để trống")
            .MinimumLength(8).WithMessage("Mật khẩu phải có ít nhất 8 ký tự")
            .MaximumLength(128).WithMessage("Mật khẩu không được vượt quá 128 ký tự")
            .Matches(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&])[A-Za-z\d@$!%*?&]")
            .WithMessage("Mật khẩu phải chứa ít nhất 1 chữ thường, 1 chữ hoa, 1 số và 1 ký tự đặc biệt");

        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Họ tên không được để trống")
            .Length(2, 100).WithMessage("Họ tên phải từ 2-100 ký tự")
            .Matches(@"^[a-zA-ZÀ-ỹ\s]+$").WithMessage("Họ tên chỉ được chứa chữ cái và khoảng trắng");

        RuleFor(x => x.Phone)
            .NotEmpty().WithMessage("Số điện thoại không được để trống")
            .Matches(@"^(0[3|5|7|8|9])+([0-9]{8})$").WithMessage("Số điện thoại không đúng định dạng Việt Nam")
            .MustAsync(async (phone, cancellation) => !await _validationService.IsPhoneExistsAsync(phone))
            .WithMessage("Số điện thoại đã được sử dụng");

        RuleFor(x => x.RoleId)
            .NotEmpty().WithMessage("Vai trò không được để trống");

        RuleFor(x => x.DepartmentId)
            .NotEmpty().WithMessage("Phòng ban không được để trống");
    }
}