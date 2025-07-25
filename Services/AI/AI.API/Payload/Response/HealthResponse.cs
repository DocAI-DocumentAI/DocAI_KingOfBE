namespace AI.API.Payload.Response
{
    public class HealthResponse
    {
        public string Status { get; set; } = "Healthy";
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string Version { get; set; } = "1.0.0";
        public string Environment { get; set; } = "Development";
        public TimeSpan Uptime { get; set; }
        public string? Error { get; set; }

        /// <summary>
        /// Additional health check details
        /// </summary>
        public Dictionary<string, object> Details { get; set; } = new();
    }

    /// <summary>
    /// Model availability and performance status
    /// </summary>
    public class ModelStatus
    {
        /// <summary>
        /// Model name
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Model type (Chat, Embedding, etc.)
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// Current status
        /// </summary>
        public string Status { get; set; } // Available, Unavailable, Testing, Degraded

        /// <summary>
        /// Last time the model was checked
        /// </summary>
        public DateTime? LastChecked { get; set; }

        /// <summary>
        /// Response time for model validation
        /// </summary>
        public int? ResponseTimeMs { get; set; }

        /// <summary>
        /// Error message if model is unavailable
        /// </summary>
        public string Error { get; set; }

        /// <summary>
        /// Model configuration details
        /// </summary>
        public object Configuration { get; set; }
    }

    /// <summary>
    /// Enhanced system metrics with detailed performance data
    /// </summary>
    public class HealthSystemMetrics
    {
        /// <summary>
        /// Total requests in the last 24 hours
        /// </summary>
        public int RequestsLast24Hours { get; set; }

        /// <summary>
        /// Successful requests count
        /// </summary>
        public int SuccessfulRequests { get; set; }

        /// <summary>
        /// Failed requests count
        /// </summary>
        public int FailedRequests { get; set; }

        /// <summary>
        /// Success rate percentage
        /// </summary>
        public double SuccessRate { get; set; }

        /// <summary>
        /// Average response time in milliseconds
        /// </summary>
        public double AverageResponseTimeMs { get; set; }

        /// <summary>
        /// Total tokens processed
        /// </summary>
        public long TotalTokensUsed { get; set; }

        /// <summary>
        /// Unique users served
        /// </summary>
        public int UniqueUsers { get; set; }

        /// <summary>
        /// Current memory usage in MB
        /// </summary>
        public double MemoryUsageMB { get; set; }

        /// <summary>
        /// CPU usage percentage
        /// </summary>
        public double CpuUsagePercent { get; set; }

        /// <summary>
        /// Active connections count
        /// </summary>
        public int ActiveConnections { get; set; }

        /// <summary>
        /// Queue length for pending requests
        /// </summary>
        public int QueueLength { get; set; }
    }

    public class DetailedHealthResponse : HealthResponse
    {
        public AI.API.Common.Utils.SystemInfo SystemInfo { get; set; } = new();
        public AI.API.Common.Utils.MemoryInfo MemoryInfo { get; set; } = new();
        public List<DependencyHealth> Dependencies { get; set; } = new();
        public HealthMetrics Metrics { get; set; } = new();
    }

    public class DependencyHealth
    {
        public string Name { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public TimeSpan Duration { get; set; }
        public Dictionary<string, string> Data { get; set; } = new();
    }

    public class HealthMetrics
    {
        public double CpuUsage { get; set; }
        public long MemoryUsage { get; set; }
        public long DiskUsage { get; set; }
        public int ActiveConnections { get; set; }
        public int RequestsPerMinute { get; set; }
        public double ErrorRate { get; set; }
    }
}
