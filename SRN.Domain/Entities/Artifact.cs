using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

public class Artifact
{
    [Key]
    public Guid ArtifactId { get; set; }

    [Required]
    [MaxLength(200)]
    public string Title { get; set; }

    [Required]
    [Column(TypeName = "char(64)")]
    public string FileHash { get; set; }

    [Required]
    public string FilePath { get; set; }

    public DateTime UploadDate { get; set; } = DateTime.UtcNow;

    public Guid OwnerId { get; set; }

    public string Status { get; set; } = "Pending Verification";

    [Column(TypeName = "varchar(66)")]
    public string? TxHash { get; set; }
}