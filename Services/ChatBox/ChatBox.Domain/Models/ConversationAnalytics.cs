using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChatBox.Domain.Models
{
    public class ConversationAnalytics
    {
        public Guid UserId { get; set; }
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public ConversationMetrics Metrics { get; set; }
        public List<TopicAnalytics> TopTopics { get; set; } = new();
        public EngagementMetrics Engagement { get; set; }
        public QualityMetrics Quality { get; set; }
        public List<DailyConversationStats> DailyStats { get; set; } = new();
        public UserBehaviorInsights BehaviorInsights { get; set; }
        public DateTime GeneratedAt { get; set; }
    }
    public class DailyConversationStats
    {
        public DateTime Date { get; set; }
        public int SessionCount { get; set; }
        public int MessageCount { get; set; }
        public int TokensUsed { get; set; }
        public TimeSpan TotalDuration { get; set; }
        public double AverageRating { get; set; }
        public int UniqueTopics { get; set; }
        public List<string> PopularTopics { get; set; } = new();
    }

    public class UserBehaviorInsights
    {
        public string PrimaryUsagePattern { get; set; } // morning, afternoon, evening, night
        public List<string> PreferredTopics { get; set; } = new();
        public double AverageSessionLength { get; set; }
        public string CommunicationStyle { get; set; } // concise, detailed, conversational
        public Dictionary<string, double> TopicDistribution { get; set; } = new();
        public TrendAnalysis UsageTrend { get; set; }
        public List<string> Recommendations { get; set; } = new();
    }

    public class TrendAnalysis
    {
        public string Direction { get; set; } // increasing, decreasing, stable
        public double ChangePercentage { get; set; }
        public string Period { get; set; }
        public List<TrendPoint> DataPoints { get; set; } = new();
    }

    public class TrendPoint
    {
        public DateTime Date { get; set; }
        public double Value { get; set; }
        public string Metric { get; set; }
    }

    public class SystemAnalytics
    {
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public OverallSystemMetrics Overall { get; set; }
        public List<UserSegmentAnalytics> UserSegments { get; set; } = new();
        public List<PerformanceMetrics> Performance { get; set; } = new();
        public List<TopicPopularity> PopularTopics { get; set; } = new();
        public List<UsagePattern> UsagePatterns { get; set; } = new();
        public ResourceUtilization ResourceUsage { get; set; }
        public DateTime GeneratedAt { get; set; }
    }

    public class OverallSystemMetrics
    {
        public int TotalUsers { get; set; }
        public int ActiveUsers { get; set; }
        public int TotalSessions { get; set; }
        public int TotalMessages { get; set; }
        public long TotalTokensUsed { get; set; }
        public TimeSpan TotalActiveTime { get; set; }
        public double AverageSessionDuration { get; set; }
        public double AverageMessagesPerSession { get; set; }
        public double SystemUptime { get; set; }
        public double UserSatisfactionScore { get; set; }
    }

    public class UserSegmentAnalytics
    {
        public string SegmentName { get; set; }
        public int UserCount { get; set; }
        public double UsagePercentage { get; set; }
        public double EngagementScore { get; set; }
        public double SatisfactionScore { get; set; }
        public List<string> CommonBehaviors { get; set; } = new();
        public Dictionary<string, object> Characteristics { get; set; } = new();
    }

    public class PerformanceMetrics
    {
        public DateTime Timestamp { get; set; }
        public double ResponseTime { get; set; }
        public double AIServiceLatency { get; set; }
        public double DocumentServiceLatency { get; set; }
        public double SystemLoad { get; set; }
        public int ConcurrentUsers { get; set; }
        public int ErrorRate { get; set; }
        public Dictionary<string, object> Details { get; set; } = new();
    }

    public class TopicPopularity
    {
        public string Topic { get; set; }
        public int MentionCount { get; set; }
        public int UniqueUsers { get; set; }
        public double PopularityScore { get; set; }
        public string Category { get; set; }
        public TrendAnalysis Trend { get; set; }
        public List<string> RelatedTopics { get; set; } = new();
    }

    public class UsagePattern
    {
        public string PatternName { get; set; }
        public string Description { get; set; }
        public double Frequency { get; set; }
        public List<string> CharacteristicBehaviors { get; set; } = new();
        public Dictionary<string, object> Statistics { get; set; } = new();
    }

    public class ResourceUtilization
    {
        public TokenUsageStats TokenUsage { get; set; }
        public StorageStats Storage { get; set; }
        public ComputeStats Compute { get; set; }
        public CostAnalysis Costs { get; set; }
    }

    public class TokenUsageStats
    {
        public long TotalTokensConsumed { get; set; }
        public long InputTokens { get; set; }
        public long OutputTokens { get; set; }
        public double AverageTokensPerMessage { get; set; }
        public List<DailyTokenUsage> DailyUsage { get; set; } = new();
        public Dictionary<string, long> ModelUsage { get; set; } = new();
    }

    public class DailyTokenUsage
    {
        public DateTime Date { get; set; }
        public long TokenCount { get; set; }
        public decimal EstimatedCost { get; set; }
    }

    public class StorageStats
    {
        public long TotalMessages { get; set; }
        public long TotalSessions { get; set; }
        public long DatabaseSize { get; set; }
        public long CacheSize { get; set; }
        public double GrowthRate { get; set; }
    }

    public class ComputeStats
    {
        public double AverageCpuUsage { get; set; }
        public double AverageMemoryUsage { get; set; }
        public int PeakConcurrentUsers { get; set; }
        public double SystemThroughput { get; set; }
    }

    public class CostAnalysis
    {
        public decimal TotalCost { get; set; }
        public decimal AIServiceCost { get; set; }
        public decimal InfrastructureCost { get; set; }
        public decimal StorageCost { get; set; }
        public decimal CostPerUser { get; set; }
        public decimal CostPerMessage { get; set; }
        public TrendAnalysis CostTrend { get; set; }
    }
}
