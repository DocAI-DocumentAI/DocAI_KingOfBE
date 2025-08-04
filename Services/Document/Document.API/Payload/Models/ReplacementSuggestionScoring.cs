namespace Document.API.Payload.Models
{
    public class ReplacementSuggestionScoring
    {
        public SemanticSimilarityScores Semantic { get; set; } = new SemanticSimilarityScores();
        public MetadataSimilarityScores Metadata { get; set; } = new MetadataSimilarityScores();
        public ContextualScores Context { get; set; } = new ContextualScores();
        public WeightedScores Final { get; set; } = new WeightedScores();
    }

    public class SemanticSimilarityScores
    {
        public double EmbeddingSimilarity { get; set; }
        public double TitleSimilarity { get; set; }
        public double DescriptionSimilarity { get; set; }
    }

    public class MetadataSimilarityScores
    {
        public double DocumentTypeMatch { get; set; }
        public double TagSimilarity { get; set; }
        public double DepartmentCompatibility { get; set; }
    }

    public class ContextualScores
    {
        public double RecencyScore { get; set; }
        public double DepartmentBonus { get; set; }
        public double StatusRelevance { get; set; }
    }

    public class WeightedScores
    {
        public double WeightedSemantic { get; set; }
        public double WeightedMetadata { get; set; }
        public double WeightedContext { get; set; }
        public double FinalScore { get; set; }
    }
}
