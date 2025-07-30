using AutoMapper;
using ChatBox.API.Payload.Request;
using ChatBox.API.Payload.Response;
using ChatBox.API.Services.Interfaces;
using ChatBox.Domain.Models;
using ChatBox.Infrastructure.Repository.Interfaces;

namespace ChatBox.API.Services.Implement
{
    public class PreferenceService : IPreferenceService
    {
        private readonly IUnitOfWork<ChatBoxDbContext> _unitOfWork;
        private readonly IMapper _mapper;

        public PreferenceService(IUnitOfWork<ChatBoxDbContext> unitOfWork, IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }

        public async Task<List<PreferenceResponse>> GetSessionPreferencesAsync(string sessionId)
        {
            var preferences = await _unitOfWork.GetRepository<SessionPreference>()
                .GetListAsync(predicate: p => p.SessionId == sessionId);

            return _mapper.Map<List<PreferenceResponse>>(preferences);
        }

        public async Task<PreferenceResponse> UpdatePreferenceAsync(string sessionId, UpdatePreferenceRequest request)
        {
            var existingPreference = await _unitOfWork.GetRepository<SessionPreference>()
                .SingleOrDefaultAsync(predicate: p => p.SessionId == sessionId && p.Key == request.Key);

            if (existingPreference != null)
            {
                existingPreference.Value = request.Value;
                existingPreference.UpdatedAt = DateTime.UtcNow;
                _unitOfWork.GetRepository<SessionPreference>().UpdateAsync(existingPreference);
            }
            else
            {
                var newPreference = new SessionPreference
                {
                    SessionId = sessionId,
                    Key = request.Key,
                    Value = request.Value
                };
                await _unitOfWork.GetRepository<SessionPreference>().InsertAsync(newPreference);
                existingPreference = newPreference;
            }

            await _unitOfWork.CommitAsync();
            return _mapper.Map<PreferenceResponse>(existingPreference);
        }

        public async Task<bool> DeletePreferenceAsync(string sessionId, string key)
        {
            var preference = await _unitOfWork.GetRepository<SessionPreference>()
                .SingleOrDefaultAsync(predicate: p => p.SessionId == sessionId && p.Key == key);

            if (preference == null)
                return false;

            _unitOfWork.GetRepository<SessionPreference>().DeleteAsync(preference);
            await _unitOfWork.CommitAsync();
            return true;
        }

        public async Task<string> GetPreferenceValueAsync(string sessionId, string key)
        {
            var preference = await _unitOfWork.GetRepository<SessionPreference>()
                .SingleOrDefaultAsync(predicate: p => p.SessionId == sessionId && p.Key == key);

            return preference?.Value;
        }
    }
}

