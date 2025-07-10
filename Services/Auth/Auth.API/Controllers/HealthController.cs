using Microsoft.AspNetCore.Mvc;

namespace Auth.API.Controllers;

[ApiController]
[Route("[controller]")]
public class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        var hostname = Environment.GetEnvironmentVariable("HOSTNAME") ?? "unknown";
        return Ok(new
        {
            status = "healthy",
            service = "Auth API",
            instance = hostname,
            timestamp = DateTime.UtcNow
        });
    }
}



