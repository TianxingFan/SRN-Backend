using Microsoft.EntityFrameworkCore;
using SRN.Domain.Entities;

namespace SRN.Infrastructure.Persistence
{
    // The core database context class used by Entity Framework to interact with the database
    public class SrnDbContext : DbContext
    {
        public SrnDbContext(DbContextOptions<SrnDbContext> options)
            : base(options)
        {
        }

        // Represents the "Artifacts" table in the database
        public DbSet<Artifact> Artifacts { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Configure schema rules and constraints using the Fluent API

            // Enforce a unique index on the 'FileHash' column at the database level.
            // This acts as a double-safety check alongside the application logic.
            modelBuilder.Entity<Artifact>()
                .HasIndex(a => a.FileHash)
                .IsUnique();

            base.OnModelCreating(modelBuilder);
        }
    }
}