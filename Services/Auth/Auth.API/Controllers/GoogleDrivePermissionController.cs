using Auth.API.Constants;
using Auth.API.Services.Interface;
using Auth.API.Utils;
using Auth.API.Attributes;
using MassTransit;
using Microsoft.AspNetCore.Mvc;
using Shared.Commands;

namespace Auth.API.Controllers
{
    /// <summary>
    /// Controller for managing Google Drive permission setup from Auth service
    /// Provides endpoints for manual permission setup and bulk operations
    /// </summary>
    [ApiController]
    [Route(ApiEndPointConstant.GoogleDrivePermission.Base)]
    public class GoogleDrivePermissionController : ControllerBase
    {
        private readonly IPublishEndpoint _publishEndpoint;
        private readonly ILogger<GoogleDrivePermissionController> _logger;

        public GoogleDrivePermissionController(
            IPublishEndpoint publishEndpoint,
            ILogger<GoogleDrivePermissionController> logger)
        {
            _publishEndpoint = publishEndpoint;
            _logger = logger;
        }

        /// <summary>
        /// Manually trigger Google Drive permission setup for a specific department
        /// </summary>
        /// <param name="departmentId">Department ID</param>
        /// <param name="departmentName">Department name (optional)</param>
        /// <returns>Success status</returns>
        [HttpPost(ApiEndPointConstant.GoogleDrivePermission.SetupDepartment)]
        [CustomAuthorize(Roles = new[] { Roles.Admin })]
        public async Task<IActionResult> SetupDepartmentPermissions([FromRoute] string departmentId, [FromQuery] string? departmentName = null)
        {
            try
            {
                _logger.LogInformation("Manually triggering Google Drive permission setup for department {DepartmentId}", departmentId);

                var command = new SetupDepartmentGoogleDrivePermissionsCommand
                {
                    DepartmentId = departmentId,
                    DepartmentName = departmentName ?? $"Department {departmentId}",
                    UserEmails = new List<string>() // Will be populated by the consumer
                };

                await _publishEndpoint.Publish(command);

                return Ok(new
                {
                    Success = true,
                    Message = $"Google Drive permission setup triggered for department {departmentId}",
                    DepartmentId = departmentId
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error triggering Google Drive permission setup for department {DepartmentId}", departmentId);
                return StatusCode(500, new
                {
                    Success = false,
                    Message = "Internal server error occurred while triggering permission setup",
                    Error = ex.Message
                });
            }
        }

        /// <summary>
        /// Manually trigger Google Drive permission setup for a specific user
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <param name="userEmail">User email</param>
        /// <param name="departmentId">User's department ID</param>
        /// <returns>Success status</returns>
        [HttpPost(ApiEndPointConstant.GoogleDrivePermission.SetupUser)]
        [CustomAuthorize(Roles = new[] { Roles.Admin })]
        public async Task<IActionResult> SetupUserPermissions([FromRoute] string userId, [FromQuery] string userEmail, [FromQuery] string departmentId)
        {
            try
            {
                _logger.LogInformation("Manually triggering Google Drive permission setup for user {UserId} ({UserEmail})", userId, userEmail);

                var command = new SetupUserGoogleDrivePermissionsCommand
                {
                    UserId = userId,
                    UserEmail = userEmail,
                    DepartmentId = departmentId,
                    DepartmentName = $"Department {departmentId}"
                };

                await _publishEndpoint.Publish(command);

                return Ok(new
                {
                    Success = true,
                    Message = $"Google Drive permission setup triggered for user {userEmail}",
                    UserId = userId,
                    UserEmail = userEmail,
                    DepartmentId = departmentId
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error triggering Google Drive permission setup for user {UserId}", userId);
                return StatusCode(500, new
                {
                    Success = false,
                    Message = "Internal server error occurred while triggering permission setup",
                    Error = ex.Message
                });
            }
        }

        /// <summary>
        /// Trigger bulk Google Drive permission initialization for all departments and users
        /// </summary>
        /// <param name="forceRecreate">Whether to recreate existing folders</param>
        /// <param name="specificDepartmentIds">Optional comma-separated list of specific department IDs</param>
        /// <returns>Success status</returns>
        [HttpPost(ApiEndPointConstant.GoogleDrivePermission.BulkInitialize)]
        [CustomAuthorize(Roles = new[] { Roles.Admin })]
        public async Task<IActionResult> InitializeBulkPermissions([FromQuery] bool forceRecreate = false, [FromQuery] string? specificDepartmentIds = null)
        {
            try
            {
                _logger.LogInformation("Manually triggering bulk Google Drive permission initialization. ForceRecreate={ForceRecreate}, SpecificDepartments={SpecificDepartments}",
                    forceRecreate, specificDepartmentIds);

                var departmentIdList = string.IsNullOrEmpty(specificDepartmentIds) 
                    ? null 
                    : specificDepartmentIds.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();

                var command = new InitializeBulkGoogleDrivePermissionsCommand
                {
                    ForceRecreate = forceRecreate,
                    SpecificDepartmentIds = departmentIdList
                };

                await _publishEndpoint.Publish(command);

                return Ok(new
                {
                    Success = true,
                    Message = "Bulk Google Drive permission initialization triggered successfully",
                    ForceRecreate = forceRecreate,
                    SpecificDepartments = departmentIdList?.Count ?? 0,
                    Note = "Check Document service logs for detailed progress and results"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error triggering bulk Google Drive permission initialization");
                return StatusCode(500, new
                {
                    Success = false,
                    Message = "Internal server error occurred while triggering bulk initialization",
                    Error = ex.Message
                });
            }
        }

        /// <summary>
        /// Get status information about Google Drive permission setup capabilities
        /// </summary>
        /// <returns>Status information</returns>
        [HttpGet(ApiEndPointConstant.GoogleDrivePermission.Status)]
        [CustomAuthorize(Roles = new[] { Roles.Admin })]
        public IActionResult GetPermissionSetupStatus()
        {
            try
            {
                return Ok(new
                {
                    Success = true,
                    Message = "Google Drive permission setup service is available",
                    Features = new
                    {
                        AutoDepartmentSetup = "Automatically setup permissions when departments are created",
                        AutoUserSetup = "Automatically setup permissions when users are created",
                        ManualDepartmentSetup = "Manually trigger permission setup for specific departments",
                        ManualUserSetup = "Manually trigger permission setup for specific users",
                        BulkInitialization = "Bulk setup permissions for all existing departments and users"
                    },
                    Endpoints = new
                    {
                        SetupDepartment = $"POST {ApiEndPointConstant.GoogleDrivePermission.Base}/{ApiEndPointConstant.GoogleDrivePermission.SetupDepartment}",
                        SetupUser = $"POST {ApiEndPointConstant.GoogleDrivePermission.Base}/{ApiEndPointConstant.GoogleDrivePermission.SetupUser}",
                        BulkInitialize = $"POST {ApiEndPointConstant.GoogleDrivePermission.Base}/{ApiEndPointConstant.GoogleDrivePermission.BulkInitialize}",
                        Status = $"GET {ApiEndPointConstant.GoogleDrivePermission.Base}/{ApiEndPointConstant.GoogleDrivePermission.Status}"
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting permission setup status");
                return StatusCode(500, new
                {
                    Success = false,
                    Message = "Internal server error occurred while getting status",
                    Error = ex.Message
                });
            }
        }
    }
}
