using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SRN.Domain.Entities
{
    // Represents the "Artifacts" table in the database
    public class Artifact
    {
        [Key]
        public Guid ArtifactId { get; set; }

        [Required]
        [MaxLength(200)]
        public string Title { get; set; }

        // The SHA-256 hash is strictly 64 hexadecimal characters long.
        // Use char(64) for storage efficiency.
        [Required]
        [Column(TypeName = "char(64)")]
        public string FileHash { get; set; }

        [Required]
        public string FilePath { get; set; }

        // Defaults to UTC time to ensure consistency across time zones
        public DateTime UploadDate { get; set; } = DateTime.UtcNow;

        // Foreign Key: Links to the User entity
        public Guid OwnerId { get; set; }

        // Navigation Property: Allows access to the related User object
        // Public User Owner { get; set; } 

        // Tracks the current workflow status (e.g., "Pending Verification", "Verified On-Chain")
        public string Status { get; set; } = "Pending Verification";
    }
}