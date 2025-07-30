using FluentValidation;
using Auth.API.Payload.Request.User;
using Auth.API.Payload.Request;

namespace Auth.API.Validators.User;

public class GenerateEmailOtpRequestValidator : AbstractValidator<GenerateEmailOtpRequest>
{
    public GenerateEmailOtpRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email không được để trống")
            .EmailAddress().WithMessage("Email không đúng định dạng")
            .MaximumLength(255).WithMessage("Email không được vượt quá 255 ký tự");
    }
}