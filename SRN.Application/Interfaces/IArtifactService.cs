using SRN.Application.DTOs;
using SRN.Domain.Entities;

namespace SRN.Application.Interfaces
{
    public interface IArtifactService
    {
        Task<(bool Success, string Message, Guid? ArtifactId, string? Hash)> UploadArtifactAsync(ArtifactUploadDto dto, string userId);
        Task<(bool Registered, string Owner, DateTime? RegisteredAt)> VerifyArtifactAsync(string fileHash);
        Task<Artifact?> GetArtifactForDownloadAsync(Guid id, string userId, bool isAdmin = false);
        Task<IEnumerable<object>> GetHistoryAsync(string userId);
        Task<IEnumerable<object>> GetPublicArtifactsAsync();
        Task<IEnumerable<object>> GetAllArtifactsForAdminAsync();
        Task<Artifact?> GetPublicArtifactForDownloadAsync(Guid id);
        Task<bool> DeleteArtifactAsync(Guid id, string userId, bool isAdmin = false);
        Task<(bool Success, string Message)> ApproveAndRegisterArtifactAsync(Guid artifactId);
    }
}