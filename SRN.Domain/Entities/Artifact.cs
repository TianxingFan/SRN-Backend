using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SRN.Domain.Entities
{
    /// <summary>
    /// The central Domain Entity representing a research document/paper in the system.
    /// Utilizes EF Core Data Annotations to enforce strict database schema constraints 
    /// and optimize storage types for cryptographic data.
    /// </summary>
    public class Artifact
    {
        [Key]
        public Guid ArtifactId { get; set; }

        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        // Optimized storage: A SHA-256 hash represented as a hex string is exactly 64 characters long
        [Required]
        [Column(TypeName = "char(64)")]
        public string FileHash { get; set; } = string.Empty;

        [Required]
        [MaxLength(500)]
        public string FilePath { get; set; } = string.Empty;

        public DateTime UploadDate { get; set; } = DateTime.UtcNow;

        [Required]
        [ForeignKey(nameof(Owner))]
        public string OwnerId { get; set; } = string.Empty;

        // Tracks the workflow state of the document (e.g., Pending Review, Processing Blockchain, Registered)
        [Required]
        [MaxLength(50)]
        public string Status { get; set; } = "Pending Blockchain";

        // Optimized storage: An Ethereum transaction hash is exactly 66 characters long (including the "0x" prefix)
        [Column(TypeName = "varchar(66)")]
        public string? TxHash { get; set; }

        // EF Core navigation property mapping back to the ApplicationUser who uploaded the document
        public virtual ApplicationUser Owner { get; set; } = null!;
    }
}