namespace AI.API.Payload.Response
{
    public class EmbeddingResponse
    {
        public List<float> Embedding { get; set; } // Vector nhúng (dạng float)
        public string ModelUsed { get; set; } // Tên mô hình embedding đã sử dụng
    }
}
