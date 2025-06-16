// using Auth.API.Constants;
// using Auth.API.Payload.Request;
// using Auth.API.Payload.Request.Member;
// using Auth.API.Payload.Response;
// using Auth.API.Services.Interface;
// using Auth.API.Validators;
// using Auth.Domain.Enums;
// using Auth.Infrastructure.Filter;
// using Microsoft.AspNetCore.Authorization;
// using Microsoft.AspNetCore.Http.HttpResults;
// using Microsoft.AspNetCore.Mvc;
//
// namespace Auth.API.Controllers;
//
// [ApiController]
// [Route(ApiEndPointConstant.ApiEndpoint)]
// public class ViewerController : ControllerBase
// {
//     private IViewerService _viewerService;
//     readonly ILogger<ViewerController> _logger;
//     
//     public ViewerController(ILogger<ViewerController> logger, IViewerService viewerService)
//     {
//         _logger = logger ?? throw new ArgumentNullException(nameof(logger));
//         _viewerService = viewerService ?? throw new ArgumentNullException(nameof(viewerService));
//     }
//     
//     [HttpGet(ApiEndPointConstant.Viewer.ViewerInformation)]
//     [ProducesResponseType(typeof(ViewerResponse), StatusCodes.Status200OK)]
//     [ProducesResponseType(typeof(ViewerResponse), StatusCodes.Status404NotFound)]
//     [ProducesResponseType(typeof(ViewerResponse), StatusCodes.Status500InternalServerError)]
//     [Authorize]
//     public async Task<IActionResult> GetViewerInformationAsync()
//     {
//         var response = await _viewerService.GetInformationOfViewerAsync();
//         return Ok(response);
//     }
//
//     [HttpGet(ApiEndPointConstant.Viewer.Viewers)]
//     [ApiExplorerSettings(IgnoreApi = false)] // False hiển thị trên swagger, true không hiểm thị trên swagger nhưng api vẫn hoạt đông nếu biết được endpoint
//     [ProducesResponseType(typeof(ViewerResponse), StatusCodes.Status200OK)]
//     [ProducesResponseType(typeof(ViewerResponse), StatusCodes.Status404NotFound)]
//     [ProducesResponseType(typeof(ViewerResponse), StatusCodes.Status500InternalServerError)]
//     // [CustomAuthorize(RoleEnum.Manager,RoleEnum.Staff)]
//     public async Task<IActionResult> GetAllViewersAsync(int page = 1, int size =  30, [FromQuery] ViewerFilter? filter = null, string? sortBy = null, bool isAsc = true)
//     {
//         var response = await _viewerService.GetAllViewersAsync(page, size, filter, sortBy, isAsc);
//         return Ok(response);
//     }
//
//     [HttpPatch(ApiEndPointConstant.Viewer.UpdateViewer)]
//     [ProducesResponseType(typeof(ViewerResponse), StatusCodes.Status200OK)]
//     [ProducesResponseType(typeof(ViewerResponse), StatusCodes.Status404NotFound)]
//     [ProducesResponseType(typeof(ViewerResponse), StatusCodes.Status400BadRequest)]
//     [ProducesResponseType(typeof(ViewerResponse), StatusCodes.Status500InternalServerError)]
//     public async Task<IActionResult> UpdateViewersAsync(UpdateViewerRequest updateViewerRequest)
//     {
//         var response = await _viewerService.UpdateViewerAsync(updateViewerRequest);
//         if (response == null)
//         {
//             _logger.LogError($"Update Viewer failed");
//             return Problem(MessageConstant.Viewer.UpdateFail);
//         }
//         _logger.LogInformation($"Update Viewer successful");
//         return Ok(response);
//     }
//
//     [HttpPatch(ApiEndPointConstant.Viewer.ResetPassword)]
//     [ProducesResponseType(typeof(ViewerResponse), StatusCodes.Status200OK)]
//     [ProducesResponseType(typeof(ViewerResponse), StatusCodes.Status404NotFound)]
//     [ProducesResponseType(typeof(ViewerResponse), StatusCodes.Status400BadRequest)]
//     [ProducesResponseType(typeof(ViewerResponse), StatusCodes.Status500InternalServerError)]
//     [Authorize]
//     public async Task<IActionResult> ResetPasswordAsync(ResetPasswordRequest resetPasswordRequest)
//     {
//         var response = await _viewerService.ResetPasswordAsync(resetPasswordRequest);
//         if (response == null)
//         {
//             _logger.LogError($"Reset password failed");
//             return Problem(MessageConstant.Viewer.ResetPasswordFail);
//         }
//         _logger.LogInformation($"Reset password successful");
//         return Ok(response);
//     }
// }