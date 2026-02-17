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
        public string Title { get; set; } = string.Empty;  // 加默认空值防 null

        [Required]
        [Column(TypeName = "char(64)")]
        public string FileHash { get; set; } = string.Empty;

        [Required]
        [MaxLength(500)]  // 新增：限制路径长度防太长
        public string FilePath { get; set; } = string.Empty;

        public DateTime UploadDate { get; set; } = DateTime.UtcNow;

        [ForeignKey(nameof(Owner))]  // 新增：外键注解，提升 EF 关系
        public Guid OwnerId { get; set; }

        [Required]
        [MaxLength(50)]  // 新增：Status 限制长度
        public string Status { get; set; } = "Pending Blockchain";  // 改默认一致 Controller

        [Column(TypeName = "varchar(66)")]
        public string? TxHash { get; set; }  // 已存在，好

        // 新增：导航属性到 Owner，便于查询
        public virtual ApplicationUser Owner { get; set; } = null!;
    }
}