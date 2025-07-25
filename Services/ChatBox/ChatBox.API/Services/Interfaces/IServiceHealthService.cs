using ChatBox.API.Payload.Response.ChatServiceResponse;
using ChatBox.API.Payload.Response.HealthMonitoringResponses;

namespace ChatBox.API.Services.Interfaces
{
    public interface IServiceHealthService
    {
        Task<SystemStatusResponse> GetSystemStatusAsync();
        Task<List<AlertResponse>> GetActiveAlertsAsync();
        Task<PerformanceMetrics> GetPerformanceMetricsAsync();
    }
}
