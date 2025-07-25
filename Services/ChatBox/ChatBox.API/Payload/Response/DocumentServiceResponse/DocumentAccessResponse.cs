namespace ChatBox.API.Payload.Response.DocumentServiceResponse
{
    public class DocumentAccessResponse
    {
        public bool HasAccess { get; set; }
        public string Reason { get; set; }
        public List<string> RequiredPermissions { get; set; } = new();
    }
}
