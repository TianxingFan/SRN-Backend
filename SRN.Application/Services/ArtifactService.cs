using Microsoft.Extensions.DependencyInjection;
using SRN.Application.DTOs;
using SRN.Application.Interfaces;
using SRN.Domain.Entities;
using SRN.Domain.Interfaces;
using System.Security.Cryptography;

namespace SRN.Application.Services
{
    public class ArtifactService : IArtifactService
    {
        private readonly IArtifactRepository _repository;
        private readonly IBlockchainService _blockchainService;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly INotificationService _notificationService;
        private readonly IFileStorageService _fileStorageService;

        public ArtifactService(
            IArtifactRepository repository,
            IBlockchainService blockchainService,
            IServiceScopeFactory scopeFactory,
            INotificationService notificationService,
            IFileStorageService fileStorageService)
        {
            _repository = repository;
            _blockchainService = blockchainService;
            _scopeFactory = scopeFactory;
            _notificationService = notificationService;
            _fileStorageService = fileStorageService;
        }

        public async Task<(bool Success, string Message, Guid? ArtifactId, string? Hash)> UploadArtifactAsync(ArtifactUploadDto dto, string userId)
        {
            string fileHash;
            using (var sha256 = SHA256.Create())
            using (var stream = dto.File.OpenReadStream())
            {
                var hashBytes = await sha256.ComputeHashAsync(stream);
                fileHash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
            }

            var existing = await _repository.GetByHashAsync(fileHash);
            if (existing != null)
                return (false, "Artifact already exists", existing.ArtifactId, fileHash);

            var filePath = await _fileStorageService.SaveFileAsync(dto.File, "artifacts");

            var artifact = new Artifact
            {
                ArtifactId = Guid.NewGuid(),
                Title = dto.Title ?? "Untitled",
                FileHash = fileHash,
                FilePath = filePath,
                UploadDate = DateTime.UtcNow,
                Status = "Pending Review",
                OwnerId = userId,
                TxHash = ""
            };

            try
            {
                await _repository.AddAsync(artifact);
                return (true, "Artifact uploaded and pending admin review", artifact.ArtifactId, fileHash);
            }
            catch (Exception ex)
            {
                var innerMsg = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                return (false, $"Database save failed: {innerMsg}", null, null);
            }
        }

        public async Task<(bool Success, string Message)> ApproveAndRegisterArtifactAsync(Guid artifactId)
        {
            var artifact = await _repository.GetByIdAsync(artifactId);
            if (artifact == null) return (false, "Artifact not found.");
            if (artifact.Status != "Pending Review") return (false, "Artifact is not in pending review state.");

            artifact.Status = "Processing Blockchain";
            await _repository.UpdateAsync(artifact);

            var currentArtifactId = artifact.ArtifactId.ToString();
            var currentFileHash = artifact.FileHash;
            var ownerId = artifact.OwnerId;

            _ = Task.Run(async () =>
            {
                using var scope = _scopeFactory.CreateScope();
                var bgRepository = scope.ServiceProvider.GetRequiredService<IArtifactRepository>();
                var bgNotifier = scope.ServiceProvider.GetRequiredService<INotificationService>();
                var bgBlockchain = scope.ServiceProvider.GetRequiredService<IBlockchainService>();

                try
                {
                    await Task.Delay(12000);

                    string txHash = await bgBlockchain.RegisterArtifactAsync(currentFileHash);

                    var artifactToUpdate = await bgRepository.GetByIdAsync(Guid.Parse(currentArtifactId));
                    if (artifactToUpdate != null)
                    {
                        artifactToUpdate.Status = "Registered";
                        artifactToUpdate.TxHash = txHash;
                        await bgRepository.UpdateAsync(artifactToUpdate);
                    }

                    await bgNotifier.SendSuccessAsync(ownerId, $"Your paper has been approved and registered! TxHash: {txHash}", currentArtifactId);
                }
                catch (Exception ex)
                {
                    var artifactToUpdate = await bgRepository.GetByIdAsync(Guid.Parse(currentArtifactId));
                    if (artifactToUpdate != null)
                    {
                        artifactToUpdate.Status = "Failed";
                        await bgRepository.UpdateAsync(artifactToUpdate);
                    }

                    await bgNotifier.SendFailureAsync(ownerId, $"Blockchain registration failed: {ex.Message}", currentArtifactId);
                }
            });

            return (true, "Approval accepted. Blockchain registration is now processing.");
        }

        public async Task<(bool Registered, string Owner, DateTime? RegisteredAt)> VerifyArtifactAsync(string fileHash)
        {
            var result = await _blockchainService.VerifyArtifactAsync(fileHash);
            if (!result.Registered)
                return (false, string.Empty, null);

            var verifyTime = DateTimeOffset.FromUnixTimeSeconds(result.Timestamp).DateTime.ToLocalTime();

            return (true, result.Owner, verifyTime);
        }

        public async Task<Artifact?> GetArtifactForDownloadAsync(Guid id, string userId)
        {
            return await _repository.GetByIdAndOwnerAsync(id, userId);
        }

        public async Task<Artifact?> GetArtifactForDownloadAsync(Guid id, string userId, bool isAdmin = false)
        {
            if (isAdmin)
            {
                return await _repository.GetByIdAsync(id);
            }
            return await _repository.GetByIdAndOwnerAsync(id, userId);
        }

        public async Task<IEnumerable<object>> GetHistoryAsync(string userId)
        {
            var artifacts = await _repository.GetHistoryByOwnerAsync(userId);
            return artifacts.Select(a => new
            {
                a.ArtifactId,
                a.Title,
                a.Status,
                a.UploadDate,
                a.FileHash,
                a.TxHash
            });
        }

        public async Task<IEnumerable<object>> GetPublicArtifactsAsync()
        {
            var artifacts = await _repository.GetPublicArtifactsAsync();
            return artifacts.Select(a => new
            {
                a.ArtifactId,
                a.Title,
                a.Status,
                a.UploadDate,
                a.FileHash,
                a.TxHash
            });
        }

        public async Task<IEnumerable<object>> GetAllArtifactsForAdminAsync()
        {
            var artifacts = await _repository.GetAllAsync();
            return artifacts.Select(a => new
            {
                a.ArtifactId,
                a.Title,
                a.Status,
                a.UploadDate,
                a.FileHash,
                a.TxHash,
                a.OwnerId
            });
        }

        public async Task<(bool IsTampered, Artifact? Artifact)> GetPublicArtifactForDownloadAsync(Guid id)
        {
            var artifact = await _repository.GetByIdAsync(id);

            if (artifact == null || artifact.Status != "Registered")
            {
                return (false, null);
            }

            var currentHash = await _fileStorageService.CalculateExistingFileHashAsync(artifact.FilePath);
            bool isTampered = currentHash == null || currentHash != artifact.FileHash;

            return (isTampered, artifact);
        }

        public async Task<bool> DeleteArtifactAsync(Guid id, string userId, bool isAdmin = false)
        {
            var artifact = isAdmin
                ? await _repository.GetByIdAsync(id)
                : await _repository.GetByIdAndOwnerAsync(id, userId);

            if (artifact == null) return false;

            var absolutePath = Path.Combine(Directory.GetCurrentDirectory(), artifact.FilePath);
            if (File.Exists(absolutePath))
            {
                File.Delete(absolutePath);
            }

            await _repository.DeleteAsync(artifact);
            return true;
        }
    }
}