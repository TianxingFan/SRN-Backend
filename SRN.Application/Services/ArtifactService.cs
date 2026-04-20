using Microsoft.Extensions.DependencyInjection;
using SRN.Application.DTOs;
using SRN.Application.Interfaces;
using SRN.Domain.Entities;
using SRN.Domain.Interfaces;
using System.Security.Cryptography;

namespace SRN.Application.Services
{
    /// <summary>
    /// The core application service responsible for orchestrating the business logic 
    /// surrounding research artifacts, including file hashing, storage, blockchain anchoring, 
    /// and tamper detection.
    /// </summary>
    public class ArtifactService : IArtifactService
    {
        private readonly IArtifactRepository _repository;
        private readonly IBlockchainService _blockchainService;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly INotificationService _notificationService;
        private readonly IFileStorageService _fileStorageService;

        // Constructor Injection ensures all required dependencies are provided by the DI container
        public ArtifactService(
            IArtifactRepository repository,
            IBlockchainService blockchainService,
            IServiceScopeFactory scopeFactory, // Required to resolve scoped services inside background threads
            INotificationService notificationService,
            IFileStorageService fileStorageService)
        {
            _repository = repository;
            _blockchainService = blockchainService;
            _scopeFactory = scopeFactory;
            _notificationService = notificationService;
            _fileStorageService = fileStorageService;
        }

        /// <summary>
        /// Handles the initial upload process: computes the cryptographic hash, checks for duplicates,
        /// persists the physical file, and creates the database record.
        /// </summary>
        public async Task<(bool Success, string Message, Guid? ArtifactId, string? Hash)> UploadArtifactAsync(ArtifactUploadDto dto, string userId)
        {
            string fileHash;

            // Compute the SHA-256 hash of the incoming file stream
            using (var sha256 = SHA256.Create())
            using (var stream = dto.File.OpenReadStream())
            {
                var hashBytes = await sha256.ComputeHashAsync(stream);
                fileHash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
            }

            // Prevent redundant uploads by checking if the hash already exists in the system
            var existing = await _repository.GetByHashAsync(fileHash);
            if (existing != null)
                return (false, "Artifact already exists", existing.ArtifactId, fileHash);

            // Persist the physical file to local storage
            var filePath = await _fileStorageService.SaveFileAsync(dto.File, "artifacts");

            // Initialize the domain entity
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
                // Safely extract the inner exception message if a database constraint fails
                var innerMsg = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                return (false, $"Database save failed: {innerMsg}", null, null);
            }
        }

        /// <summary>
        /// Admin action that approves a document and delegates the heavy blockchain transaction
        /// to a background thread to prevent blocking the HTTP response.
        /// </summary>
        public async Task<(bool Success, string Message)> ApproveAndRegisterArtifactAsync(Guid artifactId)
        {
            var artifact = await _repository.GetByIdAsync(artifactId);
            if (artifact == null) return (false, "Artifact not found.");
            if (artifact.Status != "Pending Review") return (false, "Artifact is not in pending review state.");

            // Immediately update status to provide UI feedback
            artifact.Status = "Processing Blockchain";
            await _repository.UpdateAsync(artifact);

            // Capture state variables to be safely passed into the background thread closure
            var currentArtifactId = artifact.ArtifactId.ToString();
            var currentFileHash = artifact.FileHash;
            var ownerId = artifact.OwnerId;

            // Fire-and-forget background task for blockchain anchoring
            _ = Task.Run(async () =>
            {
                // Create a new Dependency Injection scope because DbContext and Repositories are scoped per HTTP request,
                // and the original request scope will be disposed by the time this thread finishes.
                using var scope = _scopeFactory.CreateScope();
                var bgRepository = scope.ServiceProvider.GetRequiredService<IArtifactRepository>();
                var bgNotifier = scope.ServiceProvider.GetRequiredService<INotificationService>();
                var bgBlockchain = scope.ServiceProvider.GetRequiredService<IBlockchainService>();

                try
                {
                    // Simulated delay to account for network consensus/block mining time
                    await Task.Delay(12000);

                    // Execute smart contract transaction
                    string txHash = await bgBlockchain.RegisterArtifactAsync(currentFileHash);

                    var artifactToUpdate = await bgRepository.GetByIdAsync(Guid.Parse(currentArtifactId));
                    if (artifactToUpdate != null)
                    {
                        artifactToUpdate.Status = "Registered";
                        artifactToUpdate.TxHash = txHash;
                        await bgRepository.UpdateAsync(artifactToUpdate);
                    }

                    // Push real-time success notification to the author via SignalR
                    await bgNotifier.SendSuccessAsync(ownerId, $"Your paper has been approved and registered! TxHash: {txHash}", currentArtifactId);
                }
                catch (Exception ex)
                {
                    // Revert status and notify user upon transaction failure
                    var artifactToUpdate = await bgRepository.GetByIdAsync(Guid.Parse(currentArtifactId));
                    if (artifactToUpdate != null)
                    {
                        artifactToUpdate.Status = "Failed";
                        await bgRepository.UpdateAsync(artifactToUpdate);
                    }

                    await bgNotifier.SendFailureAsync(ownerId, $"Blockchain registration failed: {ex.Message}", currentArtifactId);
                }
            });

            // Return immediately to the client while the background thread processes
            return (true, "Approval accepted. Blockchain registration is now processing.");
        }

        /// <summary>
        /// Validates an arbitrary SHA-256 hash against the Ethereum smart contract registry.
        /// </summary>
        public async Task<(bool Registered, string Owner, DateTime? RegisteredAt)> VerifyArtifactAsync(string fileHash)
        {
            var result = await _blockchainService.VerifyArtifactAsync(fileHash);
            if (!result.Registered)
                return (false, string.Empty, null);

            // Convert Unix timestamp from the blockchain into a human-readable local DateTime
            var verifyTime = DateTimeOffset.FromUnixTimeSeconds(result.Timestamp).DateTime.ToLocalTime();

            return (true, result.Owner, verifyTime);
        }

        /// <summary>
        /// Retrieves a document ensuring the requester is the original owner.
        /// </summary>
        public async Task<Artifact?> GetArtifactForDownloadAsync(Guid id, string userId)
        {
            return await _repository.GetByIdAndOwnerAsync(id, userId);
        }

        /// <summary>
        /// Retrieves a document with an override mechanism allowing Administrators global read access.
        /// </summary>
        public async Task<Artifact?> GetArtifactForDownloadAsync(Guid id, string userId, bool isAdmin = false)
        {
            if (isAdmin)
            {
                return await _repository.GetByIdAsync(id);
            }
            return await _repository.GetByIdAndOwnerAsync(id, userId);
        }

        /// <summary>
        /// Maps domain entities to anonymous objects to safely send historical data to the UI 
        /// without exposing internal entity structures.
        /// </summary>
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

        /// <summary>
        /// Core Security Feature: Retrieves public documents for download and performs an 
        /// On-Demand integrity check by dynamically recalculating the physical file hash on the server.
        /// </summary>
        public async Task<(bool IsTampered, Artifact? Artifact)> GetPublicArtifactForDownloadAsync(Guid id)
        {
            var artifact = await _repository.GetByIdAsync(id);

            if (artifact == null || artifact.Status != "Registered")
            {
                return (false, null);
            }

            // Dynamically calculate the hash of the file currently sitting on the hard drive
            var currentHash = await _fileStorageService.CalculateExistingFileHashAsync(artifact.FilePath);

            // Flag as tampered if the physical file is missing or its hash no longer matches the database/blockchain record
            bool isTampered = currentHash == null || currentHash != artifact.FileHash;

            return (isTampered, artifact);
        }

        /// <summary>
        /// Executes a hard delete, completely removing both the physical file and the database record.
        /// </summary>
        public async Task<bool> DeleteArtifactAsync(Guid id, string userId, bool isAdmin = false)
        {
            var artifact = isAdmin
                ? await _repository.GetByIdAsync(id)
                : await _repository.GetByIdAndOwnerAsync(id, userId);

            if (artifact == null) return false;

            // Attempt to clean up the physical file to prevent storage bloat
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