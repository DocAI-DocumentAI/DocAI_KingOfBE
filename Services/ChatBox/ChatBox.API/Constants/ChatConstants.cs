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
        public const int MaxHistoryMessages = 10;
        public const int MinHistoryMessages = 4;
        public const int ContextValidationMessageCount = 10;
        public const int MaxChatHistoryCount = 20;
        public const int RecentMessagesCount = 10;

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
        public const int MaxTitleLength = 100;

        // Prompts
        public const string SystemPrompt = @"Bạn là trợ lý AI thông minh của công ty. Trả lời bằng tiếng Việt, thân thiện và chuyên nghiệp.

 HƯỚNG DẪN XỬ LÝ TÀI LIỆU:
Khi bạn thấy phần [=== THÔNG TIN TÀI LIỆU LIÊN QUAN ===], bạn PHẢI:
1.  Sử dụng thông tin từ tài liệu làm nguồn chính để trả lời
2.  Trích dẫn chính xác từ tài liệu được cung cấp  
3.  Không bịa đặt thông tin không có trong tài liệu
4.  Cấu trúc câu trả lời rõ ràng với nguồn tham khảo";

        public const string UserNamePromptTemplate = "Bạn có thể gọi người dùng là {0}.";
        public const string CharacteristicsPromptTemplate = "Phong cách giao tiếp của bạn nên: {0}.";
        public const string AdditionalInfoPromptTemplate = "Thông tin bổ sung về người dùng: {0}.";
        public const string DocumentSearchPromptAddition = "Bạn có thể sử dụng chức năng SearchDocuments để tìm thông tin trong tài liệu công ty khi người dùng hỏi về chính sách, quy trình, hướng dẫn.";

        public const string TitleGenerationPrompt = "Tạo một tiêu đề ngắn gọn (tối đa 10 từ) bằng tiếng Việt cho cuộc trò chuyện bắt đầu bằng tin nhắn sau: {{$input}}. Chỉ trả về tiêu đề, không thêm giải thích.";
        public const string TestSystemPrompt = "Bạn là trợ lý AI. Trả lời ngắn gọn bằng tiếng Việt.";
        public const string TestUserMessage = "Chào bạn! Test connection.";
        public const string FallbackSystemPrompt = "Bạn là trợ lý AI. Trả lời ngắn gọn bằng tiếng Việt.";
        public const string DefaultFallbackMessage = "Xin chào";
    }
}
