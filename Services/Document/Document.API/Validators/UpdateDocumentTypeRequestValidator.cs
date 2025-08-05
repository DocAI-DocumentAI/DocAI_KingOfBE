using FluentValidation;
using Document.API.Payload.Request;
using Document.API.Constants;

namespace Document.API.Validators;

public class UpdateDocumentTypeRequestValidator : AbstractValidator<UpdateDocumentTypeRequest>
{
    public UpdateDocumentTypeRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage(ValidationMessageConstant.DocumentType.NameRequired)
            .Length(ValidationConstants.DocumentTypeNameMinLength, ValidationConstants.DocumentTypeNameMaxLength)
            .WithMessage(string.Format(ValidationMessageConstant.DocumentType.NameMinLength, ValidationConstants.DocumentTypeNameMinLength))
            .WithMessage(string.Format(ValidationMessageConstant.DocumentType.NameMaxLength, ValidationConstants.DocumentTypeNameMaxLength))
            .Matches(ValidationConstants.DocumentTypeNameRegex)
            .WithMessage(ValidationMessageConstant.DocumentType.NameInvalidCharacters);

        RuleFor(x => x.Description)
            .MaximumLength(ValidationConstants.DocumentTypeDescriptionMaxLength)
            .WithMessage(string.Format(ValidationMessageConstant.DocumentType.DescriptionMaxLength, ValidationConstants.DocumentTypeDescriptionMaxLength))
            .Matches(ValidationConstants.VietnameseTextRegex)
            .WithMessage(ValidationMessageConstant.DocumentType.DescriptionInvalidCharacters)
            .When(x => !string.IsNullOrEmpty(x.Description));
    }
}
