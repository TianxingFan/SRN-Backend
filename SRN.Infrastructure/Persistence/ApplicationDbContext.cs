using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SRN.Domain.Entities;

namespace SRN.Infrastructure.Persistence
{
    /// <summary>
    /// The primary Entity Framework Core Database Context.
    /// Inherits from IdentityDbContext to seamlessly integrate ASP.NET Core Identity 
    /// tables (Users, Roles, Claims) alongside the custom application domain tables.
    /// </summary>
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        // Inject database provider options (e.g., PostgreSQL connection strings) from the DI container
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // Map the Artifact domain entity to a physical database table named "Artifacts"
        public DbSet<Artifact> Artifacts { get; set; }

        /// <summary>
        /// Hook to configure the database schema using the Fluent API.
        /// Used for complex constraints that cannot be expressed via Data Annotations alone.
        /// </summary>
        protected override void OnModelCreating(ModelBuilder builder)
        {
            // Ensure the base Identity tables are mapped correctly
            base.OnModelCreating(builder);

            builder.Entity<Artifact>(entity =>
            {
                // Explicitly define the primary key
                entity.HasKey(e => e.ArtifactId);

                // Enforce a strict unique constraint at the database schema level.
                // This guarantees that even under severe race conditions, 
                // the same file hash cannot be uploaded twice.
                entity.HasIndex(e => e.FileHash).IsUnique();
            });
        }
    }
}