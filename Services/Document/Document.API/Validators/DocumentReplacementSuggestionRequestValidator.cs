using FluentValidation;
using Document.API.Payload.Request;
using Document.API.Constants;

namespace Document.API.Validators;

public class DocumentReplacementSuggestionRequestValidator : AbstractValidator<DocumentReplacementSuggestionRequest>
{
    public DocumentReplacementSuggestionRequestValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage(ValidationMessageConstant.DocumentReplacementSuggestion.TitleRequired)
            .MaximumLength(ValidationConstants.DocumentReplacementSuggestionTitleMaxLength)
            .WithMessage(string.Format(ValidationMessageConstant.DocumentReplacementSuggestion.TitleMaxLength, 
                ValidationConstants.DocumentReplacementSuggestionTitleMaxLength));

        RuleFor(x => x.Description)
            .MaximumLength(ValidationConstants.DocumentReplacementSuggestionDescriptionMaxLength)
            .WithMessage(string.Format(ValidationMessageConstant.DocumentReplacementSuggestion.DescriptionMaxLength, 
                ValidationConstants.DocumentReplacementSuggestionDescriptionMaxLength))
            .When(x => !string.IsNullOrEmpty(x.Description));

        RuleFor(x => x.DocumentTypeId)
            .NotEmpty().WithMessage(ValidationMessageConstant.DocumentReplacementSuggestion.DocumentTypeIdRequired);

        RuleFor(x => x.MaxSuggestions)
            .InclusiveBetween(ValidationConstants.DocumentReplacementSuggestionMaxSuggestionsMin,
                ValidationConstants.DocumentReplacementSuggestionMaxSuggestionsMax)
            .WithMessage(string.Format(ValidationMessageConstant.DocumentReplacementSuggestion.MaxSuggestionsRange,
                ValidationConstants.DocumentReplacementSuggestionMaxSuggestionsMin,
                ValidationConstants.DocumentReplacementSuggestionMaxSuggestionsMax));

        RuleFor(x => x.MinSimilarityThreshold)
            .InclusiveBetween(0.0, 1.0)
            .WithMessage("Ngưỡng tương đồng tối thiểu phải từ 0.0 đến 1.0");

        // RuleFor(x => x.Tags)
        //     .Must(tags => tags == null || tags.Count <= ValidationConstants.DocumentReplacementSuggestionTagsMaxCount)
        //     .WithMessage(string.Format(ValidationMessageConstant.DocumentReplacementSuggestion.TagsMaxCount,
        //         ValidationConstants.DocumentReplacementSuggestionTagsMaxCount));

        // RuleForEach(x => x.Tags)
        //     .MaximumLength(ValidationConstants.DocumentReplacementSuggestionTagMaxLength)
        //     .WithMessage(string.Format(ValidationMessageConstant.DocumentReplacementSuggestion.TagMaxLength,
        //         ValidationConstants.DocumentReplacementSuggestionTagMaxLength))
        //     .When(x => x.Tags != null);
    }
}
