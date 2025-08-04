using Document.Domain.Model;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Document.Domain.Models
{
    /// <summary>
    /// Represents a document type entity that categorizes documents
    /// </summary>
    public class DocumentType : BaseEntity
    {
        /// <summary>
        /// The name of the document type (e.g., "Policy", "Procedure", "Manual")
        /// </summary>
        [Required]
        [StringLength(100, ErrorMessage = "Document type name must not exceed 100 characters")]
        public string Name { get; set; }

        /// <summary>
        /// Optional description of the document type
        /// </summary>
        [StringLength(500, ErrorMessage = "Description must not exceed 500 characters")]
        public string? Description { get; set; }

        /// <summary>
        /// Navigation property for documents of this type
        /// </summary>
        public virtual ICollection<DocumentFile> DocumentFiles { get; set; } = new List<DocumentFile>();
    }
}
