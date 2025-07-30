using ChatBox.API.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.Text;

namespace ChatBox.API.Plugins
{
    public class DocumentSearchPlugin
    {
        private readonly IDocumentSearchService _documentSearchService;

        public DocumentSearchPlugin(
            IDocumentSearchService documentSearchService)
        {
            _documentSearchService = documentSearchService;
        }

        [KernelFunction]
        [Description("Tìm kiếm thông tin trong tài liệu nội bộ để trả lời câu hỏi của người dùng")]
        public async Task<string> SearchDocuments(
            [Description("Câu hỏi hoặc thông tin cần tìm kiếm")] string query,
            [Description("ID người dùng")] string userId)
        {
            try
            {
                // ✅ Get RAG answer from Document microservice
                var answer = await _documentSearchService.GetRAGAnswerWithSourcesAsync(query, userId);

                return answer;
            }
            catch (Exception ex)
            {
                return "Đã xảy ra lỗi khi tìm kiếm tài liệu. Vui lòng thử lại sau.";
            }
        }

        [KernelFunction]
        [Description("Tìm kiếm thông tin trong tài liệu chính thức/official của công ty")]
        public async Task<string> SearchOfficialDocuments(
            [Description("Câu hỏi cần tìm trong tài liệu chính thức")] string query,
            [Description("ID người dùng")] string userId)
        {
            try
            {
                var result = await _documentSearchService.SearchOfficialDocumentsAsync(query, userId);

                if (result?.Success == true && !string.IsNullOrEmpty(result.Answer))
                {
                    return $"📋 **Thông tin chính thức:**\n{result.Answer}";
                }

                return "Không tìm thấy thông tin chính thức nào liên quan đến câu hỏi này.";
            }
            catch (Exception ex)
            {
                return "Đã xảy ra lỗi khi tìm kiếm tài liệu chính thức.";
            }
        }
    }
}
