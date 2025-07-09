namespace Notification.API.Payload.Response
{
    public class DocumentDetailResponseExternal
    {
        public Guid DocumentId { get; set; }
        public string Title { get; set; } = null!;
        public string Version { get; set; } = null!;
        public Guid? OwnerUserId { get; set; }
        public Guid DepartmentId { get; set; }
        public string? DepartmentName { get; set; }
        public DateTime? EffectiveUntil { get; set; }
        public string Status { get; set; } = null!;
        public string? DocumentLink { get; set; }
    }
}
