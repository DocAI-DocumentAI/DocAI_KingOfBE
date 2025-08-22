namespace ChatBox.API.Constants
{
    public class ChatConstants
    {
        public const string DefaultSessionTitle = "Cuộc trò chuyện mới";
        public const string DefaultModelName = "mistralai/mistral-small-3.2-24b-instruct:free";
        public const string TokenizerModel = "mistral";
        public const string DefaultEncodingName = "cl100k_base";

        // Token limits
        public const int MaxMessageLength = 10000;
        public const int MaxTokenLimit = 4000;
        public const double TokenWarningThreshold = 0.8;
        public const int MaxContextTokens = 16000;
        public const double ContextWarningThreshold = 0.8;

        // History management
        public const int MaxHistoryMessages = 20;      // 10 → 20 (tăng ít)
        public const int MinHistoryMessages = 6;       // 4 → 6 (tăng ít)
        public const int MaxChatHistoryCount = 50;     // 20 → 50 (tăng ít) 
        public const int RecentMessagesCount = 15;     // 10 → 15 (tăng ít)

        // Mistral specific
        public const int MistralMaxHistoryCount = 7;
        public const int MistralKeepMessageCount = 4;
        public const double MistralTokenAdjustment = 0.92;
        public const int MistralMaxTokens = 8192;
        public const int MistralMaxContextTokens = 6000;

        // Model limits
        public const int GPT4MaxTokens = 8192;
        public const int GPT4MaxContextTokens = 6000;
        public const int GPT35MaxTokens = 4096;
        public const int GPT35MaxContextTokens = 3000;
        public const int DefaultMaxTokens = 4000;
        public const int DefaultMaxContextTokens = 4000;

        // Preference limits
        public const int MaxCharacteristics = 2;
        public const int MaxAdditionalInfoLength = 200;
        public const int MaxTitleLength = 50;

        // Prompts
        public const string SystemPrompt = @"Bạn là AI Assistant của hệ thống tài liệu nội bộ công ty, chuyên hỗ trợ nhân viên tìm kiếm thông tin và trả lời câu hỏi.

NGUYÊN TẮC HOẠT ĐỘNG:
1. LUÔN ưu tiên thông tin từ tài liệu nội bộ khi có sẵn
2. Khi không có tài liệu: Không sử dụng kiến thức chung mà chỉ được giao tiếp cơ bản + thông báo rõ ràng
3. Luôn trả lời bằng tiếng Việt, chuyên nghiệp nhưng thân thiện
4. Cung cấp thông tin chính xác, chi tiết và có cấu trúc

QUY TẮC TRẢ LỜI:
✅ CÓ TÀI LIỆU NỘI BỘ:
- Dựa chính vào thông tin tài liệu
- Trích dẫn nguồn: ""Theo tài liệu [tên tài liệu]...""

❌ KHÔNG CÓ TÀI LIỆU NỘI BỘ:
- Chỉ được giao tiếp cơ bản không được lấy thông tin bên ngoài ( không sử dụng kiến thức chung)
- Gợi ý liên hệ bộ phận liên quan nếu cần thông tin chính thức

ĐỊNH DẠNG:
- Sử dụng markdown để format đẹp
- Chia thành sections rõ ràng
- Liệt kê thông tin bằng dấu gạch đầu dòng với nội dung cụ thể
- Kết thúc bằng câu hỏi hỗ trợ thêm

LƯU Ý: Hệ thống sẽ tự động tìm kiếm tài liệu cho mọi câu hỏi. Bạn chỉ cần xử lý kết quả.";


        public const string UserNamePromptTemplate = "Bạn có thể gọi người dùng là {0}.";
        public const string CharacteristicsPromptTemplate = "Phong cách giao tiếp của bạn nên: {0}.";
        public const string AdditionalInfoPromptTemplate = "Thông tin bổ sung về người dùng: {0}.";
        public const string DocumentSearchPromptAddition = "";

        public const string TitleGenerationPrompt = "Tạo một tiêu đề ngắn gọn (tối đa 10 từ) bằng tiếng Việt cho cuộc trò chuyện bắt đầu bằng tin nhắn sau: {{$input}}. Chỉ trả về tiêu đề, không thêm giải thích.";
        public const string TestSystemPrompt = "Bạn là trợ lý AI. Trả lời ngắn gọn bằng tiếng Việt.";
        public const string TestUserMessage = "Chào bạn! Test connection.";
        public const string FallbackSystemPrompt = "Bạn là trợ lý AI. Trả lời ngắn gọn bằng tiếng Việt.";
        public const string DefaultFallbackMessage = "Xin chào";

    }
}
