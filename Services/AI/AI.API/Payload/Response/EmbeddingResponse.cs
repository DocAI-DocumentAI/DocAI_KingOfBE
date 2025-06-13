namespace AI.API.Payload.Response
{
    public class EmbeddingResponse
    {
        public List<float> Embedding { get; set; } // Vector nhúng (dạng float)
        public string ModelUsed { get; set; } // Tên mô hình embedding đã sử dụng
        // Có thể thêm các trường metadata khác như thời gian xử lý, số token
    }
}
