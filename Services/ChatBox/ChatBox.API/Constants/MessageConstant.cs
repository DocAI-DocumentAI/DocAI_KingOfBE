namespace ChatBox.API.Constants
{
    public static class MessageConstant
    {
        public static class Chat
        {
            public const string SendSuccess = "Tin nhắn đã được gửi thành công";
            public const string SendFailed = "Gửi tin nhắn thất bại";
            public const string SessionCreated = "Phiên chat đã được tạo";
            public const string SessionNotFound = "Không tìm thấy phiên chat";
            public const string SessionDeleted = "Phiên chat đã được xóa";
            public const string ModelSwitched = "Đã chuyển đổi model thành công";
            public const string ModelSwitchFailed = "Chuyển đổi model thất bại";
            public const string CreateSessionFailed = "Tạo phiên chat thất bại";
            public const string GetSessionFailed = "Lấy thông tin phiên chat thất bại";
            public const string GetSessionsFailed = "Lấy danh sách phiên chat thất bại";
            public const string DeleteSessionFailed = "Xóa phiên chat thất bại";
            public const string GetModelsFailed = "Lấy danh sách model thất bại";
            public const string SuggestTitleFailed = "Tạo tiêu đề thất bại";
            public const string ValidateContextFailed = "Kiểm tra ngữ cảnh thất bại";
            public const string UpdateSessionTitleFailed = "Failed to update session title";
            public const string SessionTitleUpdated = "Session title updated successfully";

            public const string EmptyMessage = "Tin nhắn không được để trống";
            public const string MessageTooLong = "Tin nhắn quá dài. Vui lòng rút ngắn nội dung xuống dưới {0} ký tự";
            public const string TokenLimitExceeded = "Tin nhắn chứa {0} token, vượt quá giới hạn {1} token. Vui lòng rút ngắn nội dung";
            public const string TokenWarning = "Cảnh báo: Tin nhắn chứa {0} token, gần đạt giới hạn {1} token";
            public const string MessageValid = "Tin nhắn hợp lệ";
            public const string ContextTooLong = "Cuộc trò chuyện quá dài ({0} token). Vui lòng bắt đầu cuộc trò chuyện mới";
            public const string ContextWarning = "Cảnh báo: Cuộc trò chuyện đang dài ({0}/{1} token)";
        }

        public static class Admin
        {
            public const string ConfigCreated = "Cấu hình AI đã được tạo";
            public const string ConfigUpdated = "Cấu hình AI đã được cập nhật";
            public const string ConfigDeleted = "Cấu hình AI đã được xóa";
            public const string ConfigNotFound = "Không tìm thấy cấu hình AI";
            public const string ModelActivated = "Model đã được kích hoạt";
            public const string ModelDeactivated = "Model đã được tắt";
            public const string ModelSetAsDefault = "Model đã được đặt làm mặc định";
            public const string ModelTestSuccess = "Test model thành công";
            public const string CreateFailed = "Tạo cấu hình thất bại";
            public const string UpdateFailed = "Cập nhật cấu hình thất bại";
            public const string DeleteFailed = "Xóa cấu hình thất bại";
            public const string ModelExists = "Model '{0}' đã tồn tại";
            public const string ModelNotFound = "Model '{0}' không tồn tại";
            public const string ModelNotFoundOrDeactivated = "Model '{0}' không tìm thấy hoặc đã bị tắt";
            public const string CannotDeleteActiveConfig = "Không thể xóa cấu hình đang active. Hãy active model khác trước";
            public const string CannotDeleteLastConfig = "Không thể xóa cấu hình AI duy nhất trong hệ thống";
            public const string ModelInUse = "Không thể xóa model '{0}' vì đang được sử dụng trong các phiên chat";
            public const string NoActiveConfig = "Không tìm thấy cấu hình AI nào được kích hoạt";
            public const string GetStatsFailed = "Lấy thống kê hệ thống thất bại";
            public const string GetActivityFailed = "Lấy thống kê hoạt động thất bại";
            public const string GetModelUsageFailed = "Lấy thống kê sử dụng model thất bại";
            public const string TestModelFailed = "Test model thất bại";
            public const string ActivateModelFailed = "Kích hoạt model thất bại";
            public const string DeactivateModelFailed = "Tắt model thất bại";
            public const string SetDefaultModelFailed = "Đặt model mặc định thất bại";
            public const string GetConfigurationsFailed = "Lấy danh sách cấu hình AI thất bại";
            public const string CannotSetDefaultModel = "Không thể đặt model làm mặc định";
            public const string BulkUpdateSuccess = "Đã cập nhật {0} models thành công";
            public const string BulkUpdateFailed = "Cập nhật bulk models thất bại";
            public const string GetModelImpactFailed = "Lấy phân tích ảnh hưởng thất bại";
        }

        public static class Preference
        {
            public const string PreferenceUpdated = "Tùy chọn đã được cập nhật";
            public const string PreferenceDeleted = "Tùy chọn đã được xóa";
            public const string PreferenceNotFound = "Không tìm thấy tùy chọn";
            public const string UpdateFailed = "Cập nhật tùy chọn thất bại";
            public const string DeleteFailed = "Xóa tùy chọn thất bại";
            public const string GetPreferenceFailed = "Lấy tùy chọn thất bại";
        }

        public static class System
        {
            public const string UnauthorizedAccess = "Không có quyền truy cập";
            public const string UserIdNotFound = "Không tìm thấy thông tin người dùng trong token";
            public const string SystemError = "Đã xảy ra lỗi hệ thống";
            public const string InvalidRequest = "Yêu cầu không hợp lệ";
            public const string ServiceUnavailable = "Dịch vụ tạm thời không khả dụng";
        }

        public static class AI
        {
            public const string ModelNotSupported = "Provider {0} chưa được hỗ trợ";
            public const string ModelConfigInvalid = "Cấu hình model không hợp lệ";
            public const string KernelCreationFailed = "Không thể tạo kernel cho model {0}";
            public const string ResponseGenerationFailed = "Tạo response thất bại";
            public const string PluginRegistrationFailed = "Đăng ký plugin thất bại";
            public const string StreamResponseFailed = "Tạo streaming response thất bại";
            public const string TitleGenerationFailed = "Tạo tiêu đề thất bại";
        }

        public static class Document
        {
            public const string SearchTimeout = "Tìm kiếm tài liệu quá thời gian chờ";
            public const string SearchFailed = "Đã xảy ra lỗi khi tìm kiếm tài liệu";
            public const string SearchOfficialFailed = "Đã xảy ra lỗi khi tìm kiếm tài liệu chính thức";
            public const string NoDocumentFound = "Không tìm thấy thông tin phù hợp trong tài liệu để trả lời câu hỏi này";
        }
    }
}
