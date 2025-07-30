using AutoMapper;
using ChatBox.API.Payload.Request;
using ChatBox.API.Payload.Response;
using ChatBox.API.Services.Interfaces;
using ChatBox.Domain.Models;
using ChatBox.Infrastructure.Repository.Interfaces;

namespace ChatBox.API.Services.Implement
{
    public class AdminService : IAdminService
    {
        private readonly IUnitOfWork<ChatBoxDbContext> _unitOfWork;
        private readonly IMapper _mapper;

        public AdminService(IUnitOfWork<ChatBoxDbContext> unitOfWork, IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }

        public async Task<List<AIConfigurationResponse>> GetAIConfigurationsAsync()
        {
            var configs = await _unitOfWork.GetRepository<AIConfiguration>().GetListAsync();
            return _mapper.Map<List<AIConfigurationResponse>>(configs);
        }

        public async Task<AIConfigurationResponse> CreateAIConfigurationAsync(AIConfigurationRequest request, string userId)
        {
            var config = _mapper.Map<AIConfiguration>(request);
            config.CreatedBy = userId;
            config.UpdatedBy = userId;
            config.CreatedAt = DateTime.UtcNow;
            config.UpdatedAt = DateTime.UtcNow;
            await _unitOfWork.GetRepository<AIConfiguration>().InsertAsync(config);
            await _unitOfWork.CommitAsync();

            return _mapper.Map<AIConfigurationResponse>(config);
        }

        public async Task<AIConfigurationResponse> UpdateAIConfigurationAsync(string id, AIConfigurationRequest request, string userId)
        {
            var config = await _unitOfWork.GetRepository<AIConfiguration>().SingleOrDefaultAsync(predicate: x => x.Id == id);
            if (config == null)
                throw new ArgumentException("Không tìm thấy cấu hình AI.");

            _mapper.Map(request, config);
            config.UpdatedAt = DateTime.UtcNow;
            config.UpdatedBy = userId;

            _unitOfWork.GetRepository<AIConfiguration>().UpdateAsync(config);
            await _unitOfWork.CommitAsync();

            return _mapper.Map<AIConfigurationResponse>(config);
        }

        public async Task<bool> DeleteAIConfigurationAsync(string id)
        {
            var config = await _unitOfWork.GetRepository<AIConfiguration>().SingleOrDefaultAsync(predicate: x => x.Id == id);
            if (config == null)
                return false;

            _unitOfWork.GetRepository<AIConfiguration>().DeleteAsync(config);
            await _unitOfWork.CommitAsync();
            return true;
        }
    }
}