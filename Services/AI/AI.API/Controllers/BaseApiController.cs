using AI.API.Constants;
using Microsoft.AspNetCore.Mvc;

namespace AI.API.Controllers
{
    [ApiController]
    [Route(ApiEndPointConstant.API_PREFIX + "/[controller]")]
    [Produces("application/json")]
    public abstract class BaseApiController : ControllerBase
    {
        protected IActionResult HandleError(Exception ex, string message = null)
        {
            var errorResponse = new
            {
                success = false,
                message = message ?? "An error occurred",
                error = ex.Message,
                timestamp = DateTime.UtcNow,
                traceId = HttpContext.TraceIdentifier
            };

            return StatusCode(500, errorResponse);
        }

        protected IActionResult HandleNotFound(string resource, object id)
        {
            return NotFound(new
            {
                success = false,
                message = $"{resource} with ID {id} not found",
                timestamp = DateTime.UtcNow
            });
        }

        protected IActionResult HandleBadRequest(string message, object errors = null)
        {
            var response = new
            {
                success = false,
                message,
                errors,
                timestamp = DateTime.UtcNow
            };

            return BadRequest(response);
        }
    }
}