namespace ChatBox.API.Payload.Request
{
    public class SetMultipleModelsRequest
    {
        public List<string> ModelNames { get; set; } = new();

    }
}
