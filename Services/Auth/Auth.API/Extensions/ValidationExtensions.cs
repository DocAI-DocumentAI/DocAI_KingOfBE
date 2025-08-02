using FluentValidation;
using Auth.API.Constants;

namespace Auth.API.Extensions;

public static class ValidationExtensions
{
    public static IRuleBuilder<T, string> ValidateVietnamesePhone<T>(this IRuleBuilder<T, string> ruleBuilder)
    {
        return ruleBuilder
            .Matches(ValidationConstants.VietnamesePhoneRegex)
            .WithMessage("Số điện thoại không đúng định dạng Việt Nam");
    }

    public static IRuleBuilder<T, string> ValidateStrongPassword<T>(this IRuleBuilder<T, string> ruleBuilder)
    {
        return ruleBuilder
            .MinimumLength(ValidationConstants.PasswordMinLength)
            .WithMessage($"Mật khẩu phải có ít nhất {ValidationConstants.PasswordMinLength} ký tự")
            .MaximumLength(ValidationConstants.PasswordMaxLength)
            .WithMessage($"Mật khẩu không được vượt quá {ValidationConstants.PasswordMaxLength} ký tự")
            .Matches(ValidationConstants.PasswordRegex)
            .WithMessage("Mật khẩu phải chứa ít nhất 1 chữ thường, 1 chữ hoa, 1 số và 1 ký tự đặc biệt");
    }

    public static IRuleBuilder<T, string> ValidateVietnameseName<T>(this IRuleBuilder<T, string> ruleBuilder)
    {
        return ruleBuilder
            .Length(ValidationConstants.NameMinLength, ValidationConstants.NameMaxLength)
            .WithMessage($"Họ tên phải từ {ValidationConstants.NameMinLength}-{ValidationConstants.NameMaxLength} ký tự")
            .Matches(ValidationConstants.VietnameseNameRegex)
            .WithMessage("Họ tên chỉ được chứa chữ cái và khoảng trắng");
    }
}