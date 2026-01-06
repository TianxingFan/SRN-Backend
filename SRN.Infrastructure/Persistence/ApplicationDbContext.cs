using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SRN.Domain.Entities;

namespace SRN.Infrastructure.Persistence
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Artifact> Artifacts { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<Artifact>(entity =>
            {
                entity.HasKey(e => e.ArtifactId);
                entity.HasIndex(e => e.FileHash).IsUnique();
            });
        }
    }
}