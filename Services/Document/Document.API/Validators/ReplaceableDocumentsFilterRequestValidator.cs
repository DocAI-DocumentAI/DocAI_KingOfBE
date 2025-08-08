using Document.API.Constants;
using Document.API.Payload.Request;
using FluentValidation;

namespace Document.API.Validators;

public class ReplaceableDocumentsFilterRequestValidator : AbstractValidator<ReplaceableDocumentsFilterRequest>
{
    public ReplaceableDocumentsFilterRequestValidator()
    {
        RuleFor(x => x.Title)
            .MaximumLength(ValidationConstants.DocumentTitleMaxLength)
            .Matches(ValidationConstants.VietnameseTextRegex)
            .When(x => !string.IsNullOrEmpty(x.Title));

        RuleFor(x => x.Keyword)
            .MaximumLength(ValidationConstants.SemanticSearchQueryMaxLength)
            .Matches(ValidationConstants.VietnameseTextRegex)
            .When(x => !string.IsNullOrEmpty(x.Keyword));

        RuleFor(x => x)
            .Must(x => !x.FromDate.HasValue || !x.ToDate.HasValue || x.FromDate <= x.ToDate)
            .WithMessage(ValidationMessageConstant.OfficialDocumentsFilter.InvalidDateRange);

        RuleFor(x => x.DocumentTypeId)
            .MaximumLength(ValidationConstants.DocumentTypeNameMaxLength)
            .When(x => !string.IsNullOrEmpty(x.DocumentTypeId));

        RuleForEach(x => x.Tags)
            .MaximumLength(ValidationConstants.DocumentTagMaxLength)
            .Matches(ValidationConstants.TagRegex);

        RuleFor(x => x.SignedBy)
            .MaximumLength(ValidationConstants.DocumentSignedByMaxLength)
            .Matches(ValidationConstants.VietnameseTextRegex)
            .When(x => !string.IsNullOrEmpty(x.SignedBy));
    }
}
