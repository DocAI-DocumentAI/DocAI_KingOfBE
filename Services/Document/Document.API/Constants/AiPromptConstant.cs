namespace Document.API.Constants;

/// <summary>
/// Constants for AI prompts used in document analysis and processing
/// </summary>
public static class AiPromptConstant
{
    /// <summary>
    /// Prompts for document analysis and metadata extraction
    /// </summary>
    public static class DocumentAnalysis
    {
        /// <summary>
        /// Comprehensive prompt for extracting document metadata in JSON format
        /// </summary>
        public const string MetadataExtractionPrompt = @"Phân tích tài liệu và trả về một đối tượng JSON duy nhất với các khóa sau:
- ""title"": string — tiêu đề của tài liệu (required).
- ""versionName"": string — mã số/số hiệu tài liệu (bao gồm loại văn bản + mã/ký hiệu) (required).
- ""description"": string — Tạo mô tả rõ ràng, ngắn gọn (2-3 câu) giải thích tài liệu này về gì, mục đích chính và đối tượng ảnh hưởng. Tập trung vào mục tiêu và phạm vi của tài liệu.
- ""signedBy"": string — người ký văn bản ở phần CUỐI tài liệu (khối chữ ký). Bao gồm tên và (nếu có) chức danh.
  Quy tắc trích xuất:
  * Nếu chỉ có chức danh mà không rõ tên, trả về chính chức danh.
  * Ưu tiên tên viết HOA gần các chỉ dấu chữ ký; nếu nhiều tên, chọn tên sát chỉ dấu nhất.
- ""summary"": string — Phân tích tài liệu này và tạo một bản tóm tắt có cấu trúc chi tiết bằng tiếng Việt để hỗ trợ người dùng trong quá trình tạo tài liệu. Định dạng phản hồi dưới dạng HTML với các phần sau:
<h3>Điểm Chính</h3>
<ul>
<li>Liệt kê tất cả các chủ đề chính và thông tin quan trọng nhất từ tài liệu</li>
<li>Tập trung vào các khái niệm cốt lõi và phát hiện quan trọng</li>
<li>Làm nổi bật mục đích và phạm vi của tài liệu</li>
</ul>

<h3>Chi Tiết Quan Trọng</h3>
<ul>
<li>Cung cấp thông tin cụ thể: số liệu, ngày tháng, thông số kỹ thuật</li>
<li>Đề cập đến các bên liên quan, phòng ban chịu trách nhiệm</li>
<li>Bao gồm thông tin về quy trình hoặc thủ tục quan trọng</li>
<li>Nêu rõ bối cảnh và thông tin nền tảng liên quan</li>
</ul>

<h3>Hành Động Cần Thực Hiện</h3>
<ul>
<li>Liệt kê các hành động bắt buộc, thời hạn hoặc bước tiếp theo</li>
<li>Bao gồm yêu cầu tuân thủ hoặc thủ tục bắt buộc</li>
<li>Đề cập đến các bước triển khai hoặc thực hiện</li>
<li>Nếu không có hành động cụ thể, ghi: ""Không có hành động cụ thể được yêu cầu""</li>
</ul>

<h3>Kết Luận & Khuyến Nghị</h3>
<ul>
<li>Tóm tắt mục đích chính và kết quả của tài liệu</li>
<li>Đưa ra các khuyến nghị hoặc hàm ý chiến lược</li>
<li>Làm nổi bật tầm quan trọng của tài liệu đối với tổ chức</li>
<li>Đề xuất cách sử dụng hiệu quả thông tin trong tài liệu</li>
</ul>
 (tương tự định dạng Google NotebookLM).
- ""effectiveFrom"": 'yyyy-MM-dd' (hoặc null).
- ""effectiveUntil"": 'yyyy-MM-dd' (hoặc null).
- ""tags"": mảng tối đa 5 từ khóa liên quan.
Yêu cầu:
- Chỉ trả lời bằng một đối tượng JSON hợp lệ (không có giải thích thêm hoặc markdown).
- Nếu thiếu bất kỳ giá trị nào, sử dụng null.
- Đối với tóm tắt, sử dụng định dạng có cấu trúc với thẻ HTML như <b>, <ul>, <li> để định dạng tốt hơn.
- Escape tất cả dấu ngoặc kép bên trong nội dung HTML/markdown (ví dụ: `\""`).
- Chú ý đặc biệt đến phần đầu tài liệu cho versionName và phần chữ ký cho signedBy.";
    }

    /// <summary>
    /// Prompts for enhanced summary generation with structured format
    /// </summary>
    public static class SummaryGeneration
    {
        /// <summary>
        /// Enhanced prompt for generating structured document summaries during document creation workflow
        /// </summary>
        public const string StructuredSummaryPrompt = @"Phân tích tài liệu này và tạo một bản tóm tắt có cấu trúc chi tiết bằng tiếng Việt để hỗ trợ người dùng trong quá trình tạo tài liệu. Định dạng phản hồi dưới dạng HTML với các phần sau:

<h3>Điểm Chính</h3>
<ul>
<li>Liệt kê 3-5 chủ đề chính và thông tin quan trọng nhất từ tài liệu</li>
<li>Tập trung vào các khái niệm cốt lõi và phát hiện quan trọng</li>
<li>Làm nổi bật mục đích và phạm vi của tài liệu</li>
</ul>

<h3>Chi Tiết Quan Trọng</h3>
<ul>
<li>Cung cấp thông tin cụ thể: số liệu, ngày tháng, thông số kỹ thuật</li>
<li>Đề cập đến các bên liên quan, phòng ban chịu trách nhiệm</li>
<li>Bao gồm thông tin về quy trình hoặc thủ tục quan trọng</li>
<li>Nêu rõ bối cảnh và thông tin nền tảng liên quan</li>
</ul>

<h3>Hành Động Cần Thực Hiện</h3>
<ul>
<li>Liệt kê các hành động bắt buộc, thời hạn hoặc bước tiếp theo</li>
<li>Bao gồm yêu cầu tuân thủ hoặc thủ tục bắt buộc</li>
<li>Đề cập đến các bước triển khai hoặc thực hiện</li>
<li>Nếu không có hành động cụ thể, ghi: ""Không có hành động cụ thể được yêu cầu""</li>
</ul>

<h3>Kết Luận & Khuyến Nghị</h3>
<ul>
<li>Tóm tắt mục đích chính và kết quả của tài liệu</li>
<li>Đưa ra các khuyến nghị hoặc hàm ý chiến lược</li>
<li>Làm nổi bật tầm quan trọng của tài liệu đối với tổ chức</li>
<li>Đề xuất cách sử dụng hiệu quả thông tin trong tài liệu</li>
</ul>

Yêu cầu:
- Sử dụng tiếng Việt chuyên nghiệp phù hợp với tài liệu doanh nghiệp
- Mỗi phần có 2-4 điểm tối đa, ngắn gọn nhưng đầy đủ thông tin
- Đảm bảo định dạng HTML chính xác với thẻ <ul> và <li>
- Tập trung vào thông tin thực tế và có thể áp dụng
- Giới hạn tổng số từ dưới 1000 từ
- Tạo nội dung hữu ích cho người dùng trong việc hoàn thiện tài liệu";

        /// <summary>
        /// Prompt for regenerating enhanced summary during document creation workflow
        /// </summary>
        public const string RegenerateSummaryPrompt = @"Dựa trên nội dung tài liệu đã tải lên, tạo một bản tóm tắt cải tiến có cấu trúc để hỗ trợ người dùng hoàn thiện tài liệu. Sử dụng định dạng sau bằng tiếng Việt:

<h3>Điểm Chính</h3>
<ul>
<li>Xác định và liệt kê 3-5 điểm quan trọng nhất từ tài liệu</li>
<li>Tập trung vào khái niệm cốt lõi, mục tiêu chính và phát hiện chủ yếu</li>
<li>Làm rõ phạm vi và đối tượng áp dụng của tài liệu</li>
</ul>

<h3>Chi Tiết Quan Trọng</h3>
<ul>
<li>Bao gồm dữ liệu cụ thể, ngày tháng, số liệu hoặc thông số kỹ thuật</li>
<li>Đề cập đến các bên liên quan chính, phòng ban hoặc người chịu trách nhiệm</li>
<li>Thêm thông tin về quy trình hoặc quy định liên quan</li>
<li>Nêu rõ các điều kiện hoặc yêu cầu đặc biệt</li>
</ul>

<h3>Hành Động Cần Thực Hiện</h3>
<ul>
<li>Trích xuất các thời hạn, yêu cầu hoặc hành động bắt buộc</li>
<li>Bao gồm nghĩa vụ tuân thủ hoặc các bước triển khai</li>
<li>Đề cập đến quy trình phê duyệt hoặc thực hiện</li>
<li>Nếu không có hành động cụ thể, ghi: ""Không có hành động cụ thể được yêu cầu""</li>
</ul>

<h3>Kết Luận & Khuyến Nghị</h3>
<ul>
<li>Tóm tắt mục đích chính và kết quả mong đợi của tài liệu</li>
<li>Đưa ra các hàm ý chiến lược hoặc khuyến nghị thực hiện</li>
<li>Làm nổi bật tầm quan trọng của tài liệu đối với tổ chức</li>
<li>Đề xuất cách tối ưu hóa việc sử dụng thông tin trong tài liệu</li>
</ul>

Yêu cầu:
- Sử dụng tiếng Việt chuyên nghiệp phù hợp với tài liệu doanh nghiệp
- Đảm bảo mỗi phần có 2-4 điểm tối đa
- Giữ tổng số từ dưới 1000 từ
- Sử dụng định dạng HTML chính xác với thẻ <ul> và <li>
- Tạo bản tóm tắt có thể hành động và thực tế cho người dùng doanh nghiệp
- Tập trung vào việc hỗ trợ người dùng hiểu và sử dụng tài liệu hiệu quả";
    }

    /// <summary>
    /// Prompts for semantic search with AI-powered responses
    /// </summary>
    public static class SemanticSearch
    {
        /// <summary>
        /// Enhanced prompt for semantic search that provides concise conversational AI responses with document sources
        /// </summary>
        public const string ConversationalSearchPrompt = @"Trả lời ngắn gọn câu hỏi dựa trên tài liệu có liên quan. Giới hạn 2-3 câu, tập trung vào thông tin chính.

Yêu cầu:
- Trả lời trực tiếp, ngắn gọn, 2-3 câu
- Chỉ thông tin quan trọng nhất
- Tiếng Việt chuyên nghiệp
- Nếu không có thông tin: ""Không tìm thấy thông tin liên quan, nhưng đây là một số tài liệu bạn có thể quan tâm ""

Câu hỏi: {0}

Trả lời ngắn gọn:";

        /// <summary>
        /// Fallback prompt when no relevant documents are found
        /// </summary>
        public const string NoResultsPrompt = @"Không tìm thấy tài liệu liên quan đến: ""{0}"". Nhưng đây là một số tài liệu bạn có thể quan tâm.";
    }

    /// <summary>
    /// Configuration constants for AI prompts
    /// </summary>
    public static class Configuration
    {
        /// <summary>
        /// Maximum number of retry attempts for AI analysis (OPTIMIZED: Reduced for faster response)
        /// </summary>
        public const int MaxRetryAttempts = 1;

        /// <summary>
        /// Delay between retry attempts in milliseconds (OPTIMIZED: Reduced for faster response)
        /// </summary>
        public const int RetryDelayMs = 500;

        /// <summary>
        /// Phrases that indicate AI analysis failure
        /// </summary>
        public static readonly string[] FailureIndicators =
        {
            "INFO NOT FOUND",
            "không tìm thấy",
            "không thể phân tích",
            "thông tin không có sẵn"
        };
    }
}
