using ChatBox.API.Services.Interfaces;
using ChatBox.Domain.Models;
using ChatBox.Infrastructure.Repository.Interfaces;

namespace ChatBox.API.Services.Implement
{
    public class ContentFilterService : IContentFilterService
    {
        private readonly IUnitOfWork<ChatBoxDbContext> _unitOfWork;

        public ContentFilterService(IUnitOfWork<ChatBoxDbContext> unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<bool> IsContentAllowedAsync(string content)
        {
            var prohibitedWords = await GetProhibitedWordsInContentAsync(content);
            return !prohibitedWords.Any();
        }

        public async Task<List<string>> GetProhibitedWordsInContentAsync(string content)
        {
            if (string.IsNullOrEmpty(content))
                return new List<string>();

            var prohibitedWords = await _unitOfWork.GetRepository<ProhibitedWord>()
                .GetListAsync(predicate: w => w.IsActive);

            var contentLower = content.ToLower();
            var foundWords = new List<string>();

            foreach (var word in prohibitedWords)
            {
                if (contentLower.Contains(word.Word.ToLower()))
                {
                    foundWords.Add(word.Word);
                }
            }

            return foundWords;
        }
    }
}
