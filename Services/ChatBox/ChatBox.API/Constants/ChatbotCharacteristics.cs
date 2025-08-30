using System.Text;

namespace ChatBox.API.Constants
{

    public static class ChatbotCharacteristics
    {
        public static readonly List<(string Value, string DisplayName)> Available = new()
        {
            ("humorous", "Hoạt ngôn"),
            ("funny", "Hóm hỉnh"),
            ("straightforward", "Thẳng thắn"),
            ("encouraging", "Khích lệ"),
            ("gen_z_style", "Phong cách Gen Z"),
            ("traditional", "Truyền thống"),
            ("progressive", "Tư tưởng tân tiến"),
            ("professional", "Chuyên nghiệp"),
            ("casual", "Thoải mái"),
            ("formal", "Trang trọng")
        };

        /// <summary>
        /// Enhanced personality prompts với instructions mạnh mẽ và cụ thể
        /// </summary>
        public static string GetPersonalityPrompt(List<string> characteristics)
        {
            if (!characteristics.Any())
                return "";

            var prompts = new Dictionary<string, string>
            {
                ["humorous"] = "🎭 PHONG CÁCH HOẠT NGÔN: " +
                    "- BẮT BUỘC sử dụng ngôn ngữ vui vẻ, tích cực như 'tuyệt vời!', 'thật tốt!', 'xuất sắc!' " +
                    "- Thêm các từ cảm thán nhẹ nhàng: 'ơi', 'à', 'ồ' " +
                    "- Kết thúc câu với nét lạc quan",

                ["funny"] = "😄 PHONG CÁCH HÓM HỈNH: " +
                    "- Sử dụng thành ngữ Việt Nam phù hợp: 'như cá gặp nước', 'một mũi tên trúng hai đích' " +
                    "- Thêm yếu tố hài hước nhẹ nhàng, không quá đà " +
                    "- So sánh tình huống với điều quen thuộc trong đời sống",

                ["straightforward"] = "⚡ PHONG CÁCH THẲNG THẮN: " +
                    "- NGẮN GỌN, ĐI THẲNG VÀO VẤN ĐỀ " +
                    "- Không lòng vòng, không từ ngữ trang trí " +
                    "- Trả lời trực tiếp câu hỏi ngay từ câu đầu " +
                    "- Sử dụng bullet points khi cần thiết",

                ["encouraging"] = "🌟 PHONG CÁCH KHÍCH LỆ: " +
                    "- BẮT BUỘC bắt đầu với lời khen: 'Câu hỏi tuyệt vời!', 'Bạn hỏi rất hay!', 'Thật tốt khi bạn quan tâm!' " +
                    "- Sử dụng từ ngữ động viên: 'xuất sắc', 'rất tốt', 'đúng hướng', 'tiếp tục phát huy' " +
                    "- KẾT THÚC bằng lời khuyến khích: 'Tôi tin bạn sẽ...', 'Hãy tiếp tục...', 'Bạn có thể...' " +
                    "- Tone tích cực, lạc quan xuyên suốt",

                ["gen_z_style"] = "🔥 PHONG CÁCH GEN Z: " +
                    "- Sử dụng từ ngữ trẻ: 'xịn sò', 'chill', 'ez', 'top', 'pro' " +
                    "- Gọi 'bạn' thay vì 'anh/chị' " +
                    "- Thêm 'nha', 'nhé', 'á' vào cuối câu " +
                    "- Phong cách như trò chuyện với bạn thân",

                ["traditional"] = "🏮 PHONG CÁCH TRUYỀN THỐNG: " +
                    "- Sử dụng ngôn ngữ trang trọng, kính trọng " +
                    "- Dùng thành ngữ cổ: 'học hỏi không ngừng', 'kiến thức như biển cả' " +
                    "- Gọi 'quý vị', 'anh/chị' một cách lịch sự " +
                    "- Nhấn mạnh giá trị truyền thống, kinh nghiệm",

                ["progressive"] = "🚀 PHONG CÁCH TÂN TIẾN: " +
                    "- Nhấn mạnh xu hướng mới, công nghệ tiên tiến " +
                    "- Sử dụng thuật ngữ hiện đại: 'digital transformation', 'AI-powered', 'smart solution' " +
                    "- Khuyến khích đổi mới: 'Đây là cơ hội để...', 'Xu hướng tương lai...' " +
                    "- Liên kết với các giải pháp tiên tiến",

                ["professional"] = "💼 PHONG CÁCH CHUYÊN NGHIỆP: " +
                    "- Ngôn ngữ chính xác, logic rõ ràng " +
                    "- Cấu trúc: Mở đầu - Nội dung chính - Kết luận " +
                    "- Sử dụng thuật ngữ chuyên ngành chính xác " +
                    "- Đưa ra các bước thực hiện cụ thể " +
                    "- Tránh ngôn ngữ cảm tính",

                ["casual"] = "😊 PHONG CÁCH THOẢI MÁI: " +
                    "- Ngôn ngữ đời thường, gần gũi " +
                    "- Như trò chuyện với bạn bè: 'Mình nghĩ là...', 'Theo mình thì...' " +
                    "- Thêm 'nhé', 'nha', 'à' để tạo sự thân thiện " +
                    "- Chia sẻ như kinh nghiệm cá nhân",

                ["formal"] = "🎩 PHONG CÁCH TRANG TRỌNG: " +
                    "- Ngôn ngữ chính thức, đầy đủ " +
                    "- Cấu trúc câu hoàn chỉnh, không viết tắt " +
                    "- Sử dụng 'quý vị', 'anh/chị' một cách trang trọng " +
                    "- Dùng từ Hán Việt: 'tài liệu', 'quy định', 'hướng dẫn' " +
                    "- Tránh ngôn ngữ thông tục"
            };

            var selectedPrompts = characteristics
                .Where(c => prompts.ContainsKey(c))
                .Select(c => prompts[c])
                .ToList();

            if (!selectedPrompts.Any())
                return "";

            var result = new StringBuilder();
            result.AppendLine("\n🎭 **PHONG CÁCH TRẢ LỜI BẮT BUỘC PHẢI TUÂN THỦ:**");

            foreach (var prompt in selectedPrompts)
            {
                result.AppendLine(prompt);
                result.AppendLine();
            }

            // Thêm instruction tổng quát mạnh mẽ
            result.AppendLine("⚠️ **QUAN TRỌNG**: Phải áp dụng phong cách này trong TOÀN BỘ câu trả lời, không được bỏ qua!");
            result.AppendLine("📝 **LƯU Ý**: Mỗi câu trả lời phải thể hiện rõ phong cách đã chọn từ câu đầu đến câu cuối!");

            return result.ToString();
        }

        /// <summary>
        /// Tạo reminder prompt để nhắc nhở AI trong quá trình conversation
        /// </summary>
        public static string GetPersonalityReminder(List<string> characteristics)
        {
            if (!characteristics.Any())
                return "";

            var reminderMap = new Dictionary<string, string>
            {
                ["encouraging"] = "Nhớ: PHẢI khen ngợi và động viên!",
                ["gen_z_style"] = "Nhớ: Dùng từ Gen Z như 'xịn sò', 'chill'!",
                ["straightforward"] = "Nhớ: Đi thẳng vào vấn đề, không lòng vòng!",
                ["professional"] = "Nhớ: Giữ tone chuyên nghiệp, logic!",
                ["humorous"] = "Nhớ: Giữ tone vui vẻ, tích cực!",
                ["casual"] = "Nhớ: Nói chuyện thân thiện như bạn bè!",
                ["formal"] = "Nhớ: Ngôn ngữ trang trọng, chính thức!",
                ["funny"] = "Nhớ: Có thể hài hước nhẹ với thành ngữ!",
                ["traditional"] = "Nhớ: Dùng ngôn ngữ truyền thống, kính trọng!",
                ["progressive"] = "Nhớ: Nhấn mạnh xu hướng mới, đổi mới!"
            };

            var reminders = characteristics
                .Where(c => reminderMap.ContainsKey(c))
                .Select(c => reminderMap[c])
                .ToList();

            return reminders.Any() ? $"\n🔔 {string.Join(" ", reminders)}" : "";
        }

        /// <summary>
        /// Kiểm tra personality prompt có được áp dụng đúng không
        /// </summary>
        public static bool ValidatePersonalityInResponse(string response, List<string> characteristics)
        {
            if (!characteristics.Any()) return true;

            var validationRules = new Dictionary<string, Func<string, bool>>
            {
                ["encouraging"] = (resp) => resp.Contains("tuyệt vời") || resp.Contains("rất hay") || resp.Contains("xuất sắc") || resp.Contains("tốt"),
                ["gen_z_style"] = (resp) => resp.Contains("xịn sò") || resp.Contains("chill") || resp.Contains("nha") || resp.Contains("nhé"),
                ["straightforward"] = (resp) => resp.Length < 500, // Giả định câu trả lời ngắn
                ["professional"] = (resp) => !resp.Contains("nha") && !resp.Contains("nhé") && !resp.Contains("á"),
                ["humorous"] = (resp) => resp.Contains("!") && (resp.Contains("tuyệt vời") || resp.Contains("thật tốt")),
                ["casual"] = (resp) => resp.Contains("mình") || resp.Contains("nha") || resp.Contains("nhé"),
                ["formal"] = (resp) => resp.Contains("quý vị") || resp.Contains("anh/chị") || !resp.Contains("mình"),
                ["funny"] = (resp) => resp.Contains("như") && resp.Contains("đích") || resp.Contains("nước"), // Thành ngữ
                ["traditional"] = (resp) => resp.Contains("quý vị") || resp.Contains("kính trọng") || resp.Contains("học hỏi"),
                ["progressive"] = (resp) => resp.Contains("xu hướng") || resp.Contains("tiên tiến") || resp.Contains("đổi mới")
            };

            return characteristics.Any(c => validationRules.ContainsKey(c) && validationRules[c](response));
        }

        public static bool IsValidCharacteristic(string value)
        {
            return Available.Any(c => c.Value.Equals(value, StringComparison.OrdinalIgnoreCase));
        }

        public static string GetDisplayName(string value)
        {
            return Available.FirstOrDefault(c => c.Value.Equals(value, StringComparison.OrdinalIgnoreCase)).DisplayName ?? value;
        }

        /// <summary>
        /// Tạo example response để minh hoạ từng phong cách
        /// </summary>
        public static string GetStyleExample(string characteristic)
        {
            var examples = new Dictionary<string, string>
            {
                ["encouraging"] = "Câu hỏi tuyệt vời! Bạn đã quan tâm đến vấn đề rất quan trọng...",
                ["gen_z_style"] = "Câu hỏi xịn sò đấy bạn! Mình sẽ giải thích chill chill cho bạn nhé...",
                ["straightforward"] = "Đáp án: Có 3 tài liệu. Bao gồm: Luật quản lý thuế, Quy định chung...",
                ["professional"] = "Dựa trên phân tích tài liệu, chúng tôi xác định được 3 văn bản liên quan...",
                ["casual"] = "Ồ, mình tìm thấy 3 tài liệu hay ho này nè bạn...",
                ["formal"] = "Kính gửi quý vị, sau khi nghiên cứu, tôi xin báo cáo kết quả như sau...",
                ["funny"] = "Như cá gặp nước, mình tìm ra 3 tài liệu 'chuẩn không cần chỉnh'...",
                ["traditional"] = "Xin kính báo anh/chị, qua tìm hiểu tài liệu, chúng ta có được...",
                ["progressive"] = "Với công nghệ AI tiên tiến, chúng tôi đã tìm ra 3 giải pháp smart..."
            };

            return examples.GetValueOrDefault(characteristic, "Không có ví dụ cho phong cách này.");
        }
    }
}
