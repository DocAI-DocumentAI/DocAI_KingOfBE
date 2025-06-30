using Auth.API.Constants;
using Auth.API.Payload.Request.ActiveKey;
using Auth.API.Payload.Response.ActiveKey;
using Auth.API.Services.Interface;
using Auth.Infrastructure.Filter;
using Auth.Infrastructure.Paginate;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Auth.API.Controllers
{
    [ApiController]
    [Route(ApiEndPointConstant.ActiveKey.ActiveKeys)]
    public class ActiveKeyController : ControllerBase
    {
        private readonly IActiveKeyService _activeKeyService;
        private readonly ILogger<ActiveKeyController> _logger;

        public ActiveKeyController(IActiveKeyService activeKeyService, ILogger<ActiveKeyController> logger)
        {
            _activeKeyService = activeKeyService ?? throw new ArgumentNullException(nameof(activeKeyService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        [HttpPost(ApiEndPointConstant.ActiveKey.CreateActiveKey)]
        [Authorize]
        [ProducesResponseType(typeof(ActiveKeyResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(string), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(string), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> CreateActiveKey([FromBody] ActiveKeyRequest request)
        {
            try
            {
                var result = await _activeKeyService.CreateActiveKeyAsync(request);
                return CreatedAtAction(nameof(CreateActiveKey), result);
            }
            catch (BadHttpRequestException ex)
            {
                _logger.LogError("Failed to create active key: {Message}", ex.Message);
                return BadRequest(ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogError("Unauthorized access when creating active key: {Message}", ex.Message);
                return Unauthorized(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError("Error creating active key: {Message}", ex.Message);
                return Problem(ex.Message);
            }
        }

        [HttpGet(ApiEndPointConstant.ActiveKey.GetAllActiveKeys)]
        [Authorize]
        [ProducesResponseType(typeof(IPaginate<ActiveKeyListResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(string), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(string), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetAllActiveKeys(int page = 1, int size = 30,
            [FromQuery] ActiveKeyFilter? filter = null, string? sortBy = null, bool isAsc = true)
        {
            try
            {
                var result = await _activeKeyService.GetAllActiveKeysAsync(page, size, filter, sortBy, isAsc);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError("Error getting active keys: {Message}", ex.Message);
                return Problem(ex.Message);
            }
        }

        [HttpGet(ApiEndPointConstant.ActiveKey.GetActiveKeyById + "/{id}")]
        [Authorize]
        [ProducesResponseType(typeof(ActiveKeyListResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(string), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(string), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(string), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetActiveKeyById(Guid id)
        {
            try
            {
                var result = await _activeKeyService.GetActiveKeyByIdAsync(id);
                return Ok(result);
            }
            catch (BadHttpRequestException ex)
            {
                _logger.LogError("Active key not found: {Message}", ex.Message);
                return NotFound(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError("Error getting active key by id: {Message}", ex.Message);
                return Problem(ex.Message);
            }
        }

        [HttpPatch(ApiEndPointConstant.ActiveKey.UpdateActiveKey)]
        [Authorize]
        [ProducesResponseType(typeof(ActiveKeyListResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(string), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(string), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(string), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> UpdateActiveKey(Guid id, [FromBody] UpdateActiveKeyRequest request)
        {
            try
            {
                var result = await _activeKeyService.UpdateActiveKeyAsync(id, request);
                return Ok(result);
            }
            catch (BadHttpRequestException ex)
            {
                _logger.LogError("Failed to update active key: {Message}", ex.Message);
                return BadRequest(ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogError("Unauthorized access when updating active key: {Message}", ex.Message);
                return Unauthorized(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError("Error updating active key: {Message}", ex.Message);
                return Problem(ex.Message);
            }
        }

        [HttpDelete(ApiEndPointConstant.ActiveKey.DeleteActiveKey)]
        [Authorize]
        [ProducesResponseType(typeof(ActiveKeyListResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(string), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(string), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(string), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> DeleteActiveKey(Guid id)
        {
            try
            {
                var result = await _activeKeyService.DeleteActiveKeyAsync(id);
                return Ok(result);
            }
            catch (BadHttpRequestException ex)
            {
                _logger.LogError("Failed to delete active key: {Message}", ex.Message);
                return BadRequest(ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogError("Unauthorized access when deleting active key: {Message}", ex.Message);
                return Unauthorized(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError("Error deleting active key: {Message}", ex.Message);
                return Problem(ex.Message);
            }
        }
    }
}
