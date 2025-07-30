using System.Text.Json.Serialization;

namespace AI.API.Payload.Response
{
    /// <summary>
    /// Standardized error response format for API endpoints
    /// </summary>
    public class ErrorResponse
    {
        /// <summary>
        /// Machine-readable error code
        /// </summary>
        public string Code { get; set; } = "error";

        /// <summary>
        /// Human-readable error message
        /// </summary>
        public string Message { get; set; } = "An error occurred";

        /// <summary>
        /// Correlation ID for request tracing
        /// </summary>
        public string CorrelationId { get; set; }

        /// <summary>
        /// Trace ID for log correlation
        /// </summary>
        public string TraceId { get; set; }

        /// <summary>
        /// Timestamp when the error occurred
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Additional error details for debugging (null in production)
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public object Details { get; set; }

        /// <summary>
        /// Creates a new error response
        /// </summary>
        public ErrorResponse() { }

        /// <summary>
        /// Creates a new error response with specified message
        /// </summary>
        /// <param name="message">Error message</param>
        public ErrorResponse(string message) 
        {
            Message = message;
        }

        /// <summary>
        /// Creates a new error response with specified code and message
        /// </summary>
        /// <param name="code">Error code</param>
        /// <param name="message">Error message</param>
        public ErrorResponse(string code, string message)
        {
            Code = code;
            Message = message;
        }
    }
}
