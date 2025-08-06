namespace ChatBox.API.Payload.Response
{
    public class SessionDetailResponse
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string ModelName { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime LastActiveAt { get; set; }
        public List<MessageResponse> Messages { get; set; } = new();
        public List<PreferenceResponse> Preferences { get; set; } = new();

        public bool IsModelActive { get; set; }
        public bool CanSendMessages => IsModelActive;
        public string Warning => !IsModelActive
            ? "Model này đã bị admin tắt. Không thể gửi tin nhắn mới. Vui lòng tạo session mới."
            : null;
    }
}
