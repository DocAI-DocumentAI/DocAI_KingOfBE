using Document.Domain.Models;
using Document.API.Payload.Response;

namespace Document.API.Models;

/// <summary>
/// Internal model for processing semantic search candidates with scoring
/// </summary>
public class SemanticSearchCandidate
{
    public DocumentVersion DocumentVersion { get; set; } = null!;
    public double SemanticRelevance { get; set; }
    public double FinalScore { get; set; }
    public SemanticSearchScoring? Scoring { get; set; }
    public List<string> MatchingTags { get; set; } = new();
    public List<string> AppliedBoosts { get; set; } = new();
    public bool IsDepartmentMatch { get; set; }
}
