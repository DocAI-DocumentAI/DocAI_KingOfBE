using Auth.API.Constants;
using Auth.API.Enums;
using Auth.API.Filter;
using Auth.API.Payload.Request.Member;
using Auth.API.Payload.Response;
using Auth.API.Services.Interface;
using Auth.API.Validators;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Auth.API.Controllers;

[ApiController]
[Route(ApiEndPointConstant.ApiEndpoint)]
public class MemberController : ControllerBase
{
    private IMemberService _memberService;
    readonly ILogger<MemberController> _logger;
    
    public MemberController(ILogger<MemberController> logger, IMemberService memberService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _memberService = memberService ?? throw new ArgumentNullException(nameof(memberService));
    }
    
    [HttpGet(ApiEndPointConstant.Member.MemberInformation)]
    [ProducesResponseType(typeof(MemberResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MemberResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(MemberResponse), StatusCodes.Status500InternalServerError)]
    [Authorize]
    public async Task<IActionResult> GetMemberInformationAsync()
    {
        var response = await _memberService.GetInformationOfMemberAsync();
        return Ok(response);
    }

    [HttpGet(ApiEndPointConstant.Member.Members)]
    [ProducesResponseType(typeof(MemberResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MemberResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(MemberResponse), StatusCodes.Status500InternalServerError)]
    [CustomAuthorize(RoleEnum.Manager,RoleEnum.Staff)]
    public async Task<IActionResult> GetAllMembersAsync(int page = 1, int size =  30, [FromQuery] MemberFilter? filter = null, string? sortBy = null, bool isAsc = true)
    {
        var response = await _memberService.GetAllMembersAsync(page, size, filter, sortBy, isAsc);
        return Ok(response);
    }

    [HttpPatch(ApiEndPointConstant.Member.UpdateMember)]
    [ProducesResponseType(typeof(MemberResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MemberResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(MemberResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(MemberResponse), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UpdateMembersAsync(UpdateMemberRequest updateMemberRequest)
    {
        var response = await _memberService.UpdateMemberAsync(updateMemberRequest);
        if (response == null)
        {
            _logger.LogError($"Update member failed");
            return Problem(MessageConstant.Member.UpdateFail);
        }
        _logger.LogInformation($"Update member successful");
        return Ok(response);
    }
    
}