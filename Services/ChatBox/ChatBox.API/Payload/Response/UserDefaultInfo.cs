namespace ChatBox.API.Payload.Response
{
    public class UserDefaultInfo
    {
        public string UserName { get; set; } = "";
        public List<string> Characteristics { get; set; } = new();
        public string AdditionalInfo { get; set; } = "";
    }
}
