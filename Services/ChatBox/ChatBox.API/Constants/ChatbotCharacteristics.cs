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

        public static bool IsValidCharacteristic(string value)
        {
            return Available.Any(c => c.Value.Equals(value, StringComparison.OrdinalIgnoreCase));
        }

        public static string GetDisplayName(string value)
        {
            return Available.FirstOrDefault(c => c.Value.Equals(value, StringComparison.OrdinalIgnoreCase)).DisplayName ?? value;
        }
    }
}
