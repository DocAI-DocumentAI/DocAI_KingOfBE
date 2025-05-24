namespace Document.API.Payload.Request
{
    public class UpdateMetaDataReqest
    {
        public string? Title { get; set; }
        public string? Description { get; set; }
        public string? Status { get; set; }
        public DateTime? EffectiveFrom { get; set; }
        public DateTime? EffectiveUntil { get; set; }

    }
}
