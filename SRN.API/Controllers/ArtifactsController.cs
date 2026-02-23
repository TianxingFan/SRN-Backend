using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using SRN.Application.DTOs;
using SRN.API.Hubs;
using SRN.Domain.Entities;
using SRN.Domain.Interfaces;
using System.Security.Claims;
using System.Security.Cryptography;

namespace SRN.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ArtifactsController : ControllerBase
    {
        private readonly IArtifactRepository _repository; // [修改点] 换成了 Repository
        private readonly IBlockchainService _blockchainService;
        private readonly IHubContext<NotificationHub> _hubContext;
        private readonly IServiceScopeFactory _scopeFactory;

        public ArtifactsController(
            IArtifactRepository repository, // [修改点] 注入 Repository
            IBlockchainService blockchainService,
            IHubContext<NotificationHub> hubContext,
            IServiceScopeFactory scopeFactory)
        {
            _repository = repository;
            _blockchainService = blockchainService;
            _hubContext = hubContext;
            _scopeFactory = scopeFactory;
        }

        [Authorize]
        [HttpPost("upload")]
        public async Task<IActionResult> Upload([FromForm] ArtifactUploadDto uploadDto)
        {
            // --- 1. 计算哈希 ---
            string fileHash;
            using (var sha256 = SHA256.Create())
            {
                using (var stream = uploadDto.File.OpenReadStream())
                {
                    var hashBytes = await sha256.ComputeHashAsync(stream);
                    fileHash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
                }
            }

            // --- 2. 查重 ---
            // [修改点] 调用 Repository
            var existingArtifact = await _repository.GetByHashAsync(fileHash);
            if (existingArtifact != null)
            {
                return Conflict(new { message = "Artifact already exists", artifactId = existingArtifact.ArtifactId });
            }

            // --- 3. 存文件 ---
            var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "Uploads");
            if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);
            var filePath = Path.Combine(uploadsFolder, $"{Guid.NewGuid()}_{uploadDto.File.FileName}");
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await uploadDto.File.CopyToAsync(stream);
            }

            // --- 4. 存数据库 ---
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return BadRequest("Invalid user ID from token.");

            var artifact = new Artifact
            {
                ArtifactId = Guid.NewGuid(),
                Title = uploadDto.Title,
                FileHash = fileHash,
                FilePath = filePath,
                UploadDate = DateTime.UtcNow,
                Status = "Pending Blockchain",
                OwnerId = userId
            };

            // [修改点] 调用 Repository 添加数据
            await _repository.AddAsync(artifact);

            // 准备变量给后台任务
            var currentArtifactId = artifact.ArtifactId.ToString();
            var currentFileHash = fileHash;
            var currentTitle = uploadDto.Title;
            var currentUserId = userId;

            // --- 5. 启动后台任务 ---
            _ = Task.Run(async () =>
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    // [修改点] 在后台任务里解析 Repository，而不是 DbContext
                    var bgRepository = scope.ServiceProvider.GetRequiredService<IArtifactRepository>();
                    try
                    {
                        string txHash = await _blockchainService.RegisterArtifactAsync(currentFileHash);

                        var artifactToUpdate = await bgRepository.GetByIdAsync(Guid.Parse(currentArtifactId));
                        if (artifactToUpdate != null)
                        {
                            artifactToUpdate.Status = "Registered";
                            artifactToUpdate.TxHash = txHash;
                            await bgRepository.UpdateAsync(artifactToUpdate); // [修改点]
                        }

                        await _hubContext.Clients.Group(currentUserId).SendAsync("ReceiveMessage", "System",
                            $"✅ 上链成功！TxHash: {txHash}", currentArtifactId);
                    }
                    catch (Exception ex)
                    {
                        var artifactToUpdate = await bgRepository.GetByIdAsync(Guid.Parse(currentArtifactId));
                        if (artifactToUpdate != null)
                        {
                            artifactToUpdate.Status = "Failed";
                            await bgRepository.UpdateAsync(artifactToUpdate); // [修改点]
                        }
                        await _hubContext.Clients.Group(currentUserId).SendAsync("ReceiveMessage", "System",
                            $"❌ 上链失败：{ex.Message}", currentArtifactId);
                    }
                }
            });

            return Accepted(new { message = "File accepted. Processing...", artifactId = artifact.ArtifactId, status = "Pending" });
        }

        [HttpGet("verify/{fileHash}")]
        public async Task<IActionResult> Verify(string fileHash)
        {
            if (string.IsNullOrEmpty(fileHash)) return BadRequest("Hash is required.");
            var result = await _blockchainService.VerifyArtifactAsync(fileHash);
            if (result.Registered)
            {
                var verifyTime = DateTimeOffset.FromUnixTimeSeconds(result.Timestamp).DateTime.ToLocalTime();
                return Ok(new { status = "Verified ✅", details = new { ownerAddress = result.Owner, registeredAt = verifyTime } });
            }
            return NotFound(new { status = "Unverified ❌" });
        }

        [Authorize]
        [HttpGet("download/{id}")]
        public async Task<IActionResult> Download(Guid id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return BadRequest("Invalid user ID.");

            // [修改点] 调用 Repository 校验并获取数据
            var artifact = await _repository.GetByIdAndOwnerAsync(id, userId);

            if (artifact == null) return NotFound("Not found or access denied.");
            if (!System.IO.File.Exists(artifact.FilePath)) return NotFound("File missing.");

            var memory = new MemoryStream();
            using (var stream = new FileStream(artifact.FilePath, FileMode.Open)) { await stream.CopyToAsync(memory); }
            memory.Position = 0;
            return File(memory, "application/octet-stream", Path.GetFileName(artifact.FilePath));
        }

        [Authorize]
        [HttpGet("history")]
        public async Task<IActionResult> GetHistory()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return BadRequest("Invalid user ID.");

            // [修改点] 调用 Repository
            var artifacts = await _repository.GetHistoryByOwnerAsync(userId);

            var result = artifacts.Select(a => new { a.ArtifactId, a.Title, a.Status, a.UploadDate, a.FileHash, a.TxHash });
            return Ok(result);
        }
    }
}