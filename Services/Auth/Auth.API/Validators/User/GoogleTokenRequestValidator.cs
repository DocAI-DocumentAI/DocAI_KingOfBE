using FluentValidation;
using Auth.API.Payload.Request.User;
using Auth.API.DTOs.Request;

namespace Auth.API.Validators.User;

public class GoogleTokenRequestValidator : AbstractValidator<GoogleTokenRequest>
{
    public GoogleTokenRequestValidator()
    {
        RuleFor(x => x.AccessToken)
            .NotEmpty().WithMessage("Google ID Token không được để trống")
            .MinimumLength(100).WithMessage("Google ID Token không hợp lệ");
    }
}