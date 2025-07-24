using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace Document.API.Payload.Request
{
    public class CreateNewVersionDraftRequest
    {
        [Required]
        public string Title { get; set; }
        
        [Required]
        public string VersionName { get; set; }
        
        public string? Summary { get; set; }
        
        public string? SignedBy { get; set; }
        
        public DateTime? EffectiveFrom { get; set; }
        
        public DateTime? EffectiveUntil { get; set; }
        
        public List<string>? Tags { get; set; }
        
        [Required]
        public IFormFile File { get; set; }
    }
}
