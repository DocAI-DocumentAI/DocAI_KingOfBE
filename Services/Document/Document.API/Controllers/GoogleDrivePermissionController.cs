using Document.API.Constants;
using Document.API.Services.Interfaces;
using Document.API.Utils;
using Document.API.Attributes;
using Microsoft.AspNetCore.Mvc;
using Shared.Commands;

namespace Document.API.Controllers
{
    /// <summary>
    /// Controller for managing Google Drive permission setup and bulk operations
    /// Handles automatic permission assignment for departments and users
    /// </summary>
    [ApiController]
    [Route(ApiEndPointConstant.GoogleDrivePermission.Base)]
    public class GoogleDrivePermissionController : ControllerBase
    {
        private readonly IGoogleDrivePermissionSetupService _permissionSetupService;
        private readonly ILogger<GoogleDrivePermissionController> _logger;

        public GoogleDrivePermissionController(
            IGoogleDrivePermissionSetupService permissionSetupService,
            ILogger<GoogleDrivePermissionController> logger)
        {
            _permissionSetupService = permissionSetupService;
            _logger = logger;
        }

        /// <summary>
        /// Setup Google Drive permissions for a specific department
        /// Creates department folders and grants access to all department users
        /// </summary>
        /// <param name="request">Department permission setup request</param>
        /// <returns>Setup result with success/failure details</returns>
        [HttpPost(ApiEndPointConstant.GoogleDrivePermission.SetupDepartment)]
        [CustomAuthorize(Roles = new[] { Roles.Admin, Roles.Manager })]
        public async Task<IActionResult> SetupDepartmentPermissions([FromBody] SetupDepartmentGoogleDrivePermissionsCommand request)
        {
            try
            {
                _logger.LogInformation("Setting up Google Drive permissions for department {DepartmentId} ({DepartmentName}) with {UserCount} users",
                    request.DepartmentId, request.DepartmentName, request.UserEmails.Count);

                var result = await _permissionSetupService.SetupDepartmentPermissionsAsync(
                    request.DepartmentId,
                    request.DepartmentName,
                    request.UserEmails);

                if (result.Success)
                {
                    return Ok(new
                    {
                        Success = true,
                        Message = result.Message,
                        Details = new
                        {
                            DepartmentId = request.DepartmentId,
                            DepartmentName = request.DepartmentName,
                            TotalUsers = result.TotalUsers,
                            SuccessfulPermissions = result.SuccessfulPermissions,
                            FailedPermissions = result.FailedPermissions,
                            CreatedFolders = result.CreatedFolders
                        }
                    });
                }
                else
                {
                    return BadRequest(new
                    {
                        Success = false,
                        Message = result.Message,
                        Errors = result.Errors,
                        Details = new
                        {
                            TotalUsers = result.TotalUsers,
                            SuccessfulPermissions = result.SuccessfulPermissions,
                            FailedPermissions = result.FailedPermissions
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting up department permissions for {DepartmentId}", request.DepartmentId);
                return StatusCode(500, new
                {
                    Success = false,
                    Message = "Internal server error occurred while setting up department permissions",
                    Error = ex.Message
                });
            }
        }

        /// <summary>
        /// Setup Google Drive permissions for a specific user
        /// Grants access to their department folders and public folders
        /// </summary>
        /// <param name="request">User permission setup request</param>
        /// <returns>Setup result with success/failure details</returns>
        [HttpPost(ApiEndPointConstant.GoogleDrivePermission.SetupUser)]
        [CustomAuthorize(Roles = new[] { Roles.Admin, Roles.Manager })]
        public async Task<IActionResult> SetupUserPermissions([FromBody] SetupUserGoogleDrivePermissionsCommand request)
        {
            try
            {
                _logger.LogInformation("Setting up Google Drive permissions for user {UserEmail} in department {DepartmentId}",
                    request.UserEmail, request.DepartmentId);

                var result = await _permissionSetupService.SetupUserPermissionsAsync(
                    request.UserEmail,
                    request.DepartmentId,
                    request.DepartmentName);

                if (result.Success)
                {
                    return Ok(new
                    {
                        Success = true,
                        Message = result.Message,
                        Details = new
                        {
                            UserEmail = request.UserEmail,
                            DepartmentId = request.DepartmentId,
                            DepartmentName = request.DepartmentName,
                            SuccessfulPermissions = result.SuccessfulPermissions
                        }
                    });
                }
                else
                {
                    return BadRequest(new
                    {
                        Success = false,
                        Message = result.Message,
                        Errors = result.Errors,
                        Details = new
                        {
                            UserEmail = request.UserEmail,
                            FailedPermissions = result.FailedPermissions
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting up user permissions for {UserEmail}", request.UserEmail);
                return StatusCode(500, new
                {
                    Success = false,
                    Message = "Internal server error occurred while setting up user permissions",
                    Error = ex.Message
                });
            }
        }

        /// <summary>
        /// Perform bulk initialization of Google Drive permissions for all existing departments and users
        /// Creates all necessary folder structures and assigns permissions
        /// </summary>
        /// <param name="request">Bulk initialization request</param>
        /// <returns>Bulk setup result with comprehensive details</returns>
        [HttpPost(ApiEndPointConstant.GoogleDrivePermission.BulkInitialize)]
        [CustomAuthorize(Roles = new[] { Roles.Admin })]
        public async Task<IActionResult> InitializeBulkPermissions([FromBody] InitializeBulkGoogleDrivePermissionsCommand request)
        {
            try
            {
                _logger.LogInformation("Starting bulk Google Drive permission initialization. ForceRecreate={ForceRecreate}, SpecificDepartments={SpecificDepartments}",
                    request.ForceRecreate, request.SpecificDepartmentIds?.Count ?? 0);

                var result = await _permissionSetupService.InitializeBulkPermissionsAsync(
                    request.ForceRecreate,
                    request.SpecificDepartmentIds);

                if (result.Success)
                {
                    return Ok(new
                    {
                        Success = true,
                        Message = result.Message,
                        Summary = new
                        {
                            TotalUsers = result.TotalUsers,
                            SuccessfulPermissions = result.SuccessfulPermissions,
                            FailedPermissions = result.FailedPermissions,
                            CreatedFolders = result.CreatedFolders.Count,
                            ProcessedDepartments = request.SpecificDepartmentIds?.Count ?? 0
                        },
                        Details = new
                        {
                            CreatedFolders = result.CreatedFolders,
                            Errors = result.Errors.Take(10).ToList() // Limit errors in response
                        }
                    });
                }
                else
                {
                    return BadRequest(new
                    {
                        Success = false,
                        Message = result.Message,
                        Summary = new
                        {
                            TotalUsers = result.TotalUsers,
                            SuccessfulPermissions = result.SuccessfulPermissions,
                            FailedPermissions = result.FailedPermissions,
                            ErrorCount = result.Errors.Count
                        },
                        Errors = result.Errors.Take(20).ToList() // Limit errors in response
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during bulk Google Drive permission initialization");
                return StatusCode(500, new
                {
                    Success = false,
                    Message = "Internal server error occurred during bulk initialization",
                    Error = ex.Message
                });
            }
        }

        /// <summary>
        /// Validate Google Drive setup and permissions for a specific department
        /// </summary>
        /// <param name="departmentId">Department ID to validate</param>
        /// <returns>Validation result with details</returns>
        [HttpGet(ApiEndPointConstant.GoogleDrivePermission.ValidateDepartment)]
        [CustomAuthorize(Roles = new[] { Roles.Admin, Roles.Manager })]
        public async Task<IActionResult> ValidateDepartmentSetup([FromRoute] string departmentId)
        {
            try
            {
                _logger.LogInformation("Validating Google Drive setup for department {DepartmentId}", departmentId);

                var result = await _permissionSetupService.ValidateDepartmentSetupAsync(departmentId);

                return Ok(new
                {
                    Success = result.Success,
                    Message = result.Message,
                    DepartmentId = departmentId,
                    ValidationDetails = new
                    {
                        HasErrors = result.Errors.Any(),
                        ErrorCount = result.Errors.Count,
                        Errors = result.Errors
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating department setup for {DepartmentId}", departmentId);
                return StatusCode(500, new
                {
                    Success = false,
                    Message = "Internal server error occurred during validation",
                    Error = ex.Message
                });
            }
        }
    }
}
