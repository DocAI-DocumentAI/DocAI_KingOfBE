using FluentValidation;
using Document.API.Payload.Request;
using Document.API.Constants;

namespace Document.API.Validators;

/// <summary>
/// Validator for semantic search requests following project validation patterns
/// </summary>
public class SemanticSearchRequestValidator : AbstractValidator<SemanticSearchRequest>
{
    public SemanticSearchRequestValidator()
    {
        RuleFor(x => x.Query)
            .NotEmpty().WithMessage(ValidationMessageConstant.SemanticSearch.QueryRequired)
            .Length(ValidationConstants.SemanticSearchQueryMinLength, ValidationConstants.SemanticSearchQueryMaxLength)
            .WithMessage(string.Format(ValidationMessageConstant.SemanticSearch.QueryLengthRange, 
                ValidationConstants.SemanticSearchQueryMinLength, ValidationConstants.SemanticSearchQueryMaxLength))
            .Matches(ValidationConstants.VietnameseTextRegex)
            .WithMessage(ValidationMessageConstant.SemanticSearch.QueryInvalidCharacters);

        RuleFor(x => x.MinRelevance)
            .InclusiveBetween(ValidationConstants.SemanticSearchMinRelevanceMin, ValidationConstants.SemanticSearchMinRelevanceMax)
            .WithMessage(string.Format(ValidationMessageConstant.SemanticSearch.MinRelevanceRange, 
                ValidationConstants.SemanticSearchMinRelevanceMin, ValidationConstants.SemanticSearchMinRelevanceMax));

        RuleFor(x => x.MaxResults)
            .InclusiveBetween(ValidationConstants.SemanticSearchMaxResultsMin, ValidationConstants.SemanticSearchMaxResultsMax)
            .WithMessage(string.Format(ValidationMessageConstant.SemanticSearch.MaxResultsRange, 
                ValidationConstants.SemanticSearchMaxResultsMin, ValidationConstants.SemanticSearchMaxResultsMax));

        RuleFor(x => x.Scope)
            .IsInEnum()
            .WithMessage(ValidationMessageConstant.SemanticSearch.InvalidScope);

        // Optional filter validations
        RuleFor(x => x.DocumentTypeId)
            .Matches(ValidationConstants.VietnameseTextRegex)
            .WithMessage(ValidationMessageConstant.Document.DocumentTypeInvalid)
            .When(x => !string.IsNullOrEmpty(x.DocumentTypeId));

        RuleFor(x => x.SignedBy)
            .MaximumLength(ValidationConstants.DocumentSignedByMaxLength)
            .WithMessage(string.Format(ValidationMessageConstant.Document.SignedByMaxLength, ValidationConstants.DocumentSignedByMaxLength))
            .Matches(ValidationConstants.VietnameseTextRegex)
            .WithMessage(ValidationMessageConstant.Document.SignedByInvalidCharacters)
            .When(x => !string.IsNullOrEmpty(x.SignedBy));

        RuleFor(x => x.FromDate)
            .LessThanOrEqualTo(x => x.ToDate)
            .WithMessage("Ngày bắt đầu phải nhỏ hơn hoặc bằng ngày kết thúc")
            .When(x => x.FromDate.HasValue && x.ToDate.HasValue);

        RuleFor(x => x.EffectiveFrom)
            .LessThanOrEqualTo(x => x.EffectiveUntil)
            .WithMessage("Ngày hiệu lực bắt đầu phải nhỏ hơn hoặc bằng ngày hiệu lực kết thúc")
            .When(x => x.EffectiveFrom.HasValue && x.EffectiveUntil.HasValue);
    }


}
