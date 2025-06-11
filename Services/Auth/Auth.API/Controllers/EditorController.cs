using Auth.API.Constants;
using Auth.API.Payload.Request.Staff;
using Auth.API.Payload.Response.Staff;
using Auth.API.Services.Interface;
using Auth.API.Validators;
using Auth.Domain.Enums;
using Auth.Infrastructure.Filter;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Auth.API.Controllers;
[ApiController]
[Route(ApiEndPointConstant.ApiEndpoint)]
public class EditorController : ControllerBase
{
    private IEditorService _editorService;
    private readonly ILogger<EditorController> _logger;

    public EditorController(IEditorService editorService, ILogger<EditorController> logger)
    {
        _editorService = editorService;
        _logger = logger;
    }

    [HttpGet(ApiEndPointConstant.Editor.Editors)]
    [ProducesResponseType(typeof(EditorResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(EditorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(EditorResponse), StatusCodes.Status500InternalServerError)]
    [CustomAuthorize(RoleEnum.Editor)]
    public async Task<IActionResult> GetAllEditorsAsync(int page = 1, int size = 30,[FromQuery] EditorFilter? filter = null, string? sortBy =null, bool isAsc = true)
    {
        var response = await _editorService.GetAllEditorsAsync(page, size, filter, sortBy, isAsc);
        return Ok(response);
    }

    [HttpGet(ApiEndPointConstant.Editor.EditorInformation)]
    [ProducesResponseType(typeof(EditorResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(EditorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(EditorResponse), StatusCodes.Status500InternalServerError)]
    [CustomAuthorize(RoleEnum.Editor, RoleEnum.Admin)]
    [Authorize]
    public async Task<IActionResult> GetEditorInformationAsync()
    {
        var response = await _editorService.GetEditorInformationAsync();
        return Ok(response);
    }
    [HttpPatch(ApiEndPointConstant.Editor.UpdateEditor)]
    [ProducesResponseType(typeof(EditorResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(EditorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(EditorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(EditorResponse), StatusCodes.Status500InternalServerError)]
    [CustomAuthorize(RoleEnum.Editor, RoleEnum.Admin)]
    public async Task<IActionResult> UpdateEditorAsync(UpdateEditorRequest updateEditorRequest)
    {
        var response = await _editorService.UpdateEditorAsync(updateEditorRequest);
        if (response == null)
        {
            _logger.LogError($"Update member failed");
            return Problem(MessageConstant.Editor.UpdateFail);
        }
        _logger.LogInformation($"Update member successful");
        return Ok(response);
    }
}