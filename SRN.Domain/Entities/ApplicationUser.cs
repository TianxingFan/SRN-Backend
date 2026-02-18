using Microsoft.AspNetCore.Identity;

namespace SRN.Domain.Entities
{
    public class ApplicationUser : IdentityUser
    {
        // New field for storing blockchain wallet address
        public string? WalletAddress { get; set; }

        public string Role { get; set; } = "Researcher";

        // One-to-Many relationship: One user has many Artifacts
        // This helps EF Core understand the relationship defined in Artifact.cs
        public virtual ICollection<Artifact> Artifacts { get; set; } = new List<Artifact>();
    }
}