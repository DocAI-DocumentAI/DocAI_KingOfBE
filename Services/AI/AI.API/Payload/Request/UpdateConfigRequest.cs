namespace AI.API.Payload.Request
{
    public class UpdateConfigRequest
    {
        public string Value { get; set; }
        public string? Category { get; set; }
        public string? Description { get; set; }
    }
}
