using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SRN.Domain.Entities
{
    public class Artifact
    {
        [Key]
        public Guid ArtifactId { get; set; }

        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        [Required]
        [Column(TypeName = "char(64)")]
        public string FileHash { get; set; } = string.Empty;

        [Required]
        [MaxLength(500)]
        public string FilePath { get; set; } = string.Empty;

        public DateTime UploadDate { get; set; } = DateTime.UtcNow;

        // ⚠️ 修改点：从 Guid 改为 string，以匹配 IdentityUser 的默认主键类型
        [Required]
        [ForeignKey(nameof(Owner))]
        public string OwnerId { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string Status { get; set; } = "Pending Blockchain";

        [Column(TypeName = "varchar(66)")]
        public string? TxHash { get; set; }

        public virtual ApplicationUser Owner { get; set; } = null!;
    }
}