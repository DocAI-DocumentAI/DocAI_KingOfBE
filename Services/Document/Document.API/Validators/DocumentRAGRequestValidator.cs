using FluentValidation;
using Document.API.Payload.Request;
using Document.API.Constants;

namespace Document.API.Validators;

public class DocumentRAGRequestValidator : AbstractValidator<DocumentRAGRequest>
{
    public DocumentRAGRequestValidator()
    {
        RuleFor(x => x.Query)
            .NotEmpty().WithMessage(ValidationMessageConstant.DocumentRAG.QueryRequired)
            .Length(ValidationConstants.DocumentRAGQueryMinLength, ValidationConstants.DocumentRAGQueryMaxLength)
            .WithMessage(string.Format(ValidationMessageConstant.DocumentRAG.QueryMinLength, ValidationConstants.DocumentRAGQueryMinLength))
            .WithMessage(string.Format(ValidationMessageConstant.DocumentRAG.QueryMaxLength, ValidationConstants.DocumentRAGQueryMaxLength));

        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage(ValidationMessageConstant.DocumentRAG.UserIdRequired);

        RuleFor(x => x.MaxResults)
            .InclusiveBetween(ValidationConstants.DocumentRAGMaxResultsMin, ValidationConstants.DocumentRAGMaxResultsMax)
            .WithMessage(string.Format(ValidationMessageConstant.DocumentRAG.MaxResultsRange, 
                ValidationConstants.DocumentRAGMaxResultsMin, ValidationConstants.DocumentRAGMaxResultsMax));

        RuleFor(x => x.MinRelevanceScore)
            .InclusiveBetween(ValidationConstants.DocumentRAGMinRelevanceScoreMin, ValidationConstants.DocumentRAGMinRelevanceScoreMax)
            .WithMessage(string.Format(ValidationMessageConstant.DocumentRAG.MinRelevanceScoreRange, 
                ValidationConstants.DocumentRAGMinRelevanceScoreMin, ValidationConstants.DocumentRAGMinRelevanceScoreMax))
            .When(x => x.MinRelevanceScore.HasValue);

        RuleFor(x => x.Tags)
            .Must(tags => tags == null || tags.Count <= ValidationConstants.DocumentRAGTagsMaxCount)
            .WithMessage(string.Format(ValidationMessageConstant.DocumentRAG.TagsMaxCount, ValidationConstants.DocumentRAGTagsMaxCount));

        RuleForEach(x => x.Tags)
            .MaximumLength(ValidationConstants.DocumentRAGTagMaxLength)
            .WithMessage(string.Format(ValidationMessageConstant.DocumentRAG.TagMaxLength, ValidationConstants.DocumentRAGTagMaxLength))
            .When(x => x.Tags != null);
    }
}
