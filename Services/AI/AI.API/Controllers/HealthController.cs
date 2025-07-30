using AI.API.Common.Utils;
using AI.API.Payload.Response;
using Microsoft.AspNetCore.Mvc;

namespace AI.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class HealthController : ControllerBase
    {
        private readonly ILogger<HealthController> _logger;

        public HealthController(ILogger<HealthController> logger)
        {
            _logger = logger;
        }

        [HttpGet]
        public ActionResult<object> GetHealth()
        {
            try
            {
                var systemInfo = SystemInfoHelper.GetSystemInfo();

                var response = new
                {
                    Status = "Healthy",
                    Timestamp = DateTime.UtcNow,
                    Version = systemInfo.ApplicationVersion,
                    Environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development",
                    Uptime = systemInfo.Uptime
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Health check failed");

                var errorResponse = new
                {
                    Status = "Unhealthy",
                    Timestamp = DateTime.UtcNow,
                    Error = ex.Message
                };

                return StatusCode(503, errorResponse);
            }
        }

        [HttpGet("ready")]
        public ActionResult GetReadiness()
        {
            return Ok(new { status = "ready", timestamp = DateTime.UtcNow });
        }

        [HttpGet("live")]
        public ActionResult GetLiveness()
        {
            return Ok(new { status = "alive", timestamp = DateTime.UtcNow });
        }
    }
}
