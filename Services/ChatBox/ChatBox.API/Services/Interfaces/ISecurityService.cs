using ChatBox.API.Payload.Response.SecurityServiceResponse;
using ChatBox.API.Services.Implement;

namespace ChatBox.API.Services.Interfaces
{
    public interface  ISecurityService
    {
        Task<SecurityAnalysisResult> AnalyzeContentAsync(string content, Guid userId, string ipAddress);
        Task<PIIDetectionResult> DetectPIIAsync(string content);
        Task<List<SecurityEvent>> GetSecurityEventsAsync(Guid userId, DateTime? fromDate = null);
    }
}
