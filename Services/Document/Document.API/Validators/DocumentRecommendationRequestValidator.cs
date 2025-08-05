using FluentValidation;
using Document.API.Payload.Request;
using Document.API.Constants;

namespace Document.API.Validators;

public class DocumentRecommendationRequestValidator : AbstractValidator<DocumentRecommendationRequest>
{
    public DocumentRecommendationRequestValidator()
    {
        RuleFor(x => x.Count)
            .InclusiveBetween(ValidationConstants.DocumentRecommendationCountMin, ValidationConstants.DocumentRecommendationCountMax)
            .WithMessage(string.Format(ValidationMessageConstant.DocumentRecommendation.CountRange, 
                ValidationConstants.DocumentRecommendationCountMin, ValidationConstants.DocumentRecommendationCountMax));

        RuleFor(x => x.DocumentTypeFilter)
            .NotEmpty()
            .WithMessage(ValidationMessageConstant.DocumentRecommendation.DocumentTypeFilterInvalid)
            .When(x => !string.IsNullOrEmpty(x.DocumentTypeFilter));
    }
}
