using AutoMapper;
using Document.Infrastructure.Repository.Interfaces;
using System.Security.Authentication;
using System.Security.Claims;

namespace Document.API.Services
{
    public class BaseService<T> where T : class
    {
        protected IUnitOfWork _unitOfWork;
        protected ILogger<T> _logger;
        protected IMapper _mapper;
        protected IHttpContextAccessor _httpContextAccessor;
        protected IConfiguration _configuration;

        public BaseService(
            IUnitOfWork unitOfWork,
            ILogger<T> logger,
            IMapper mapper,
            IHttpContextAccessor httpContextAccessor,
            IConfiguration configuration)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
            _mapper = mapper;
            _httpContextAccessor = httpContextAccessor;
            _configuration = configuration;
        }

        protected string GetUserIdFromJwt()
        {
            var user = _httpContextAccessor?.HttpContext?.User;

            if (user == null || !user.Identity.IsAuthenticated)
            {
                _logger.LogWarning("User is not authenticated");
                throw new AuthenticationException("User is not authenticated.");
            }

            var userIdClaim = user.FindFirst("userId");

            if (userIdClaim == null || string.IsNullOrEmpty(userIdClaim.Value))
            {
                _logger.LogError("userId claim not found in token");
                throw new AuthenticationException("User ID claim not found in token.");
            }

            return userIdClaim.Value;
        }

        protected string GetDepartmentFromJwt()
        {
            var user = _httpContextAccessor?.HttpContext?.User;

            if (user == null || !user.Identity.IsAuthenticated)
            {
                return string.Empty;
            }

            var departmentClaim = user.FindFirst("departmentID");
            return departmentClaim?.Value ?? string.Empty;
        }

        protected string GetRoleFromJwt()
        {
            var user = _httpContextAccessor?.HttpContext?.User;

            if (user == null)
            {
                return string.Empty;
            }

            var roleClaim = user.FindFirst(ClaimTypes.Role);
            return roleClaim?.Value ?? string.Empty;
        }

        /// <summary>
        /// Check if user is admin (configurable for testing)
        /// </summary>
        protected bool IsAdminUser()
        {
            // Configurable admin access for testing
            //var allowAdminAccess = _configuration.GetValue<bool>("DocumentRAG:AllowAdminAccess", false);

            //if (!allowAdminAccess)
            //{
            //    return false;
            //}

            var role = GetRoleFromJwt();
            return role.Equals("Admin", StringComparison.OrdinalIgnoreCase);
        }
    }
}