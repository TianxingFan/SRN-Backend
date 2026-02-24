using Microsoft.EntityFrameworkCore;
using SRN.Domain.Entities;
using SRN.Domain.Interfaces;
using SRN.Infrastructure.Persistence;

namespace SRN.Infrastructure.Repositories
{
    public class ArtifactRepository : IArtifactRepository
    {
        private readonly ApplicationDbContext _context;

        public ArtifactRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<Artifact?> GetByIdAsync(Guid id)
        {
            return await _context.Artifacts.FindAsync(id);
        }

        public async Task<Artifact?> GetByHashAsync(string fileHash)
        {
            return await _context.Artifacts.FirstOrDefaultAsync(a => a.FileHash == fileHash);
        }

        public async Task<Artifact?> GetByIdAndOwnerAsync(Guid id, string ownerId)
        {
            return await _context.Artifacts.FirstOrDefaultAsync(a => a.ArtifactId == id && a.OwnerId == ownerId);
        }

        public async Task<IEnumerable<Artifact>> GetHistoryByOwnerAsync(string ownerId)
        {
            return await _context.Artifacts
                .Where(a => a.OwnerId == ownerId)
                .OrderByDescending(a => a.UploadDate)
                .ToListAsync();
        }

        public async Task<IEnumerable<Artifact>> GetPublicArtifactsAsync()
        {
            return await _context.Artifacts
                .Where(a => a.Status == "Registered")
                .OrderByDescending(a => a.UploadDate)
                .ToListAsync();
        }

        public async Task AddAsync(Artifact artifact)
        {
            _context.Artifacts.Add(artifact);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateAsync(Artifact artifact)
        {
            _context.Artifacts.Update(artifact);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(Artifact artifact)
        {
            _context.Artifacts.Remove(artifact);
            await _context.SaveChangesAsync();
        }
    }
}