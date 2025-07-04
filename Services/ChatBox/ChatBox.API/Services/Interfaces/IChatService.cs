using ChatBox.API.Payload.Request;
using ChatBox.API.Payload.Response;

namespace ChatBox.API.Services.Interfaces;

    public interface IChatService
{
    Task<ConversationResponse> StartNewConversationAsync(string userId, ChatRequestPayload request);

    // Lấy danh sách các cuộc hội thoại của người dùng
    Task<List<ConversationSummaryResponse>> GetUserConversationsAsync(string userId);

    // Lấy lịch sử tin nhắn của một cuộc hội thoại cụ thể
    Task<List<MessageResponse>> GetConversationHistoryAsync(string conversationId, string userId); // Thêm userId để kiểm tra quyền truy cập

    // Tiếp tục chat trong một cuộc hội thoại đã có (bao gồm lịch sử và RAG)
    Task<ChatResponse> ContinueChatAsync(string conversationId, string userId, ChatRequestPayload request);
    IAsyncEnumerable<string> StreamContinueChatAsync(string conversationId, string userId, ChatRequestPayload request);

    // Xóa một cuộc hội thoại
    Task<bool> DeleteConversationAsync(string conversationId, string userId); // Thêm userId để kiểm tra quyền truy cập
}
