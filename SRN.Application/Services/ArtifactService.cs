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
        private readonly INotificationService _notificationService; // 🌟 使用通知服务

        public ArtifactService(
            IArtifactRepository repository,
            IBlockchainService blockchainService,
            IServiceScopeFactory scopeFactory,
            INotificationService notificationService) // 🌟 注入
        {
            _repository = repository;
            _blockchainService = blockchainService;
            _scopeFactory = scopeFactory;
            _notificationService = notificationService;
        }

        public async Task<(bool Success, string Message, Guid? ArtifactId, string? Hash)> UploadArtifactAsync(
            ArtifactUploadDto dto,
            string userId,
            string uploadsFolder)
        {
            // 1. 计算 Hash
            string fileHash;
            using (var sha256 = SHA256.Create())
            using (var stream = dto.File.OpenReadStream())
            {
                var hashBytes = await sha256.ComputeHashAsync(stream);
                fileHash = BitConverter.ToString(hashBytes)
                    .Replace("-", "")
                    .ToLowerInvariant();
            }

            // 2. 查重
            var existing = await _repository.GetByHashAsync(fileHash);
            if (existing != null)
                return (false, "Artifact already exists", existing.ArtifactId, fileHash);

            // 3. 存文件
            if (!Directory.Exists(uploadsFolder))
                Directory.CreateDirectory(uploadsFolder);

            var filePath = Path.Combine(
                uploadsFolder,
                $"{Guid.NewGuid()}_{dto.File.FileName}"
            );

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await dto.File.CopyToAsync(stream);
            }

            // 4. 存数据库
            var artifact = new Artifact
            {
                ArtifactId = Guid.NewGuid(),
                Title = dto.Title,
                FileHash = fileHash,
                FilePath = filePath,
                UploadDate = DateTime.UtcNow,
                Status = "Pending Blockchain",
                OwnerId = userId
            };

            await _repository.AddAsync(artifact);

            var currentArtifactId = artifact.ArtifactId.ToString();
            var currentFileHash = fileHash;

            // 5. 后台执行区块链上链
            _ = Task.Run(async () =>
            {
                using var scope = _scopeFactory.CreateScope();
                var bgRepository = scope.ServiceProvider.GetRequiredService<IArtifactRepository>();
                var bgNotifier = scope.ServiceProvider.GetRequiredService<INotificationService>();

                try
                {
                    string txHash = await _blockchainService.RegisterArtifactAsync(currentFileHash);

                    var artifactToUpdate = await bgRepository.GetByIdAsync(Guid.Parse(currentArtifactId));
                    if (artifactToUpdate != null)
                    {
                        artifactToUpdate.Status = "Registered";
                        artifactToUpdate.TxHash = txHash;
                        await bgRepository.UpdateAsync(artifactToUpdate);
                    }

                    // 🌟 成功通知
                    await bgNotifier.SendSuccessAsync(
                        userId,
                        $"✅ 上链成功！TxHash: {txHash}",
                        currentArtifactId
                    );
                }
                catch (Exception ex)
                {
                    var artifactToUpdate = await bgRepository.GetByIdAsync(Guid.Parse(currentArtifactId));
                    if (artifactToUpdate != null)
                    {
                        artifactToUpdate.Status = "Failed";
                        await bgRepository.UpdateAsync(artifactToUpdate);
                    }

                    // 🌟 失败通知
                    await bgNotifier.SendFailureAsync(
                        userId,
                        $"❌ 上链失败：{ex.Message}",
                        currentArtifactId
                    );
                }
            });

            return (true, "Processing", artifact.ArtifactId, fileHash);
        }

        public async Task<(bool Registered, string Owner, DateTime? RegisteredAt)> VerifyArtifactAsync(string fileHash)
        {
            var result = await _blockchainService.VerifyArtifactAsync(fileHash);
            if (!result.Registered)
                return (false, string.Empty, null);

            var verifyTime = DateTimeOffset
                .FromUnixTimeSeconds(result.Timestamp)
                .DateTime
                .ToLocalTime();

            return (true, result.Owner, verifyTime);
        }

        public async Task<Artifact?> GetArtifactForDownloadAsync(Guid id, string userId)
        {
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
    }
}