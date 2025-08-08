using AutoMapper;
using Document.API.Utils;
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
            if (!JwtTokenHelper.IsAuthenticated(_httpContextAccessor))
            {
                _logger.LogWarning("User is not authenticated");
                throw new AuthenticationException("User is not authenticated.");
            }

            try
            {
                return JwtTokenHelper.GetUserId(_httpContextAccessor);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogError("userId claim not found in token");
                throw new AuthenticationException("User ID claim not found in token.", ex);
            }
        }

        protected string GetDepartmentFromJwt()
        {
            if (!JwtTokenHelper.IsAuthenticated(_httpContextAccessor))
            {
                return string.Empty;
            }

            // Note: Using GetDepartmentIdOrNull to handle both "departmentId" and "departmentID" claims
            return JwtTokenHelper.GetDepartmentIdOrNull(_httpContextAccessor) ?? string.Empty;
        }

        protected string GetRoleFromJwt()
        {
            return JwtTokenHelper.GetUserRole(_httpContextAccessor);
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