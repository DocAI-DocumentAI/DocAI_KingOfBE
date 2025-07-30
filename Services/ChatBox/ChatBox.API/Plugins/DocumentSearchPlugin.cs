using ChatBox.API.Services.Interfaces;
using Microsoft.SemanticKernel;
using System.ComponentModel;

namespace ChatBox.API.Plugins
{
    public class DocumentSearchPlugin
    {
        private readonly IDocumentSearchService _documentSearchService;

        public DocumentSearchPlugin(IDocumentSearchService documentSearchService)
        {
            _documentSearchService = documentSearchService;
        }

        [KernelFunction]
        [Description("Tìm kiếm tài liệu nội bộ dựa trên từ khóa")]
        public async Task<string> SearchDocuments(
            [Description("Từ khóa tìm kiếm")] string query,
            [Description("Số lượng kết quả tối đa")] int limit = 5)
        {
            var documents = await _documentSearchService.SearchDocumentsAsync(query, limit);

            if (!documents.Any())
                return "Không tìm thấy tài liệu nào phù hợp.";

            return $"Tìm thấy {documents.Count} tài liệu:\n" + string.Join("\n", documents);
        }

        [KernelFunction]
        [Description("Lấy nội dung chi tiết của tài liệu")]
        public async Task<string> GetDocumentContent(
            [Description("ID của tài liệu")] string documentId)
        {
            var content = await _documentSearchService.GetDocumentContentAsync(documentId);

            if (string.IsNullOrEmpty(content))
                return "Không thể lấy nội dung tài liệu.";

            return content;
        }
    }
}
