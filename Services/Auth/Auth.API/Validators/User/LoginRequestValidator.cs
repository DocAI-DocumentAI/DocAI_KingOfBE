using FluentValidation;
using Auth.API.Payload.Request.User;
using Auth.API.Payload.Request;

namespace Auth.API.Validators.User;

public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email không được để trống")
            .EmailAddress().WithMessage("Email không đúng định dạng");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Mật khẩu không được để trống")
            .MinimumLength(1).WithMessage("Mật khẩu không được để trống");
    }
}