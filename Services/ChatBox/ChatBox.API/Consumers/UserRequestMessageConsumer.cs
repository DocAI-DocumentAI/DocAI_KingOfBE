//using MassTransit;
//using Shared.DTOs;

//namespace ChatBox.API.Consumers;

//public class UserRequestMessageConsumer : IConsumer<UserRequestMessage>
//{
//    private readonly ILogger<UserRequestMessageConsumer> _logger;

//    public UserRequestMessageConsumer(ILogger<UserRequestMessageConsumer> logger)
//    {
//        _logger = logger;
//    }

//    public Task Consume(ConsumeContext<UserRequestMessage> context)
//    {
//        // Log ra để xác nhận message đã được nhận và xử lý
//        _logger.LogInformation("=====>>> Đã nhận được UserRequestMessage với Id: {UserId}", context.Message.id);
            
//        // Tại đây bạn có thể viết logic để xử lý message, ví dụ: gọi một service khác, cập nhật database, v.v.
            
//        return Task.CompletedTask;
//    }
//}