using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SRN.Infrastructure.Persistence;
using SRN.Infrastructure.Blockchain;
using System.Security.Cryptography;
using SRN.API.DTOs;
using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;
using SRN.API.Hubs;
using Microsoft.EntityFrameworkCore;

namespace SRN.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ArtifactsController : ControllerBase
    {
        private readonly ApplicationDbContext _context; // 仅供同步代码使用
        private readonly IBlockchainService _blockchainService;
        private readonly IHubContext<NotificationHub> _hubContext;
        private readonly IServiceScopeFactory _scopeFactory; // 1. 新增这个工厂

        public ArtifactsController(
            ApplicationDbContext context,
            IBlockchainService blockchainService,
            IHubContext<NotificationHub> hubContext,
            IServiceScopeFactory scopeFactory) // 2. 注入它
        {
            _context = context;
            _blockchainService = blockchainService;
            _hubContext = hubContext;
            _scopeFactory = scopeFactory;
        }

        [Authorize]  // 恢复 [Authorize]，生产用
        [HttpPost("upload")]
        public async Task<IActionResult> Upload([FromForm] ArtifactUploadDto uploadDto)
        {
            if (uploadDto.File == null || uploadDto.File.Length == 0)
                return BadRequest("No file uploaded.");

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

            // --- 2. 查重 (这里用 _context 没问题，因为还没返回) ---
            var existingArtifact = _context.Artifacts.FirstOrDefault(a => a.FileHash == fileHash);
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

            // --- 4. 存数据库 (初始状态) ---
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);  // 从 JWT 取 OwnerId
            if (!Guid.TryParse(userIdString, out Guid ownerId))
            {
                return BadRequest("Invalid user ID from token.");
            }

            var artifact = new Artifact
            {
                ArtifactId = Guid.NewGuid(),
                Title = uploadDto.Title,
                FileHash = fileHash,
                FilePath = filePath,
                UploadDate = DateTime.UtcNow,
                Status = "Pending Blockchain",
                OwnerId = ownerId
                // TxHash 暂时不赋值，等上链成功再更新
            };

            _context.Artifacts.Add(artifact);
            await _context.SaveChangesAsync();

            // 保存这些变量，供后台任务使用
            var currentArtifactId = artifact.ArtifactId.ToString();  // 转为 string 以匹配推送
            var currentFileHash = fileHash;
            var currentTitle = uploadDto.Title;
            var currentUserId = ownerId.ToString();  // 新增：用户 ID 用于针对推送

            // --- 5. 启动后台任务 (核心修改点) ---
            _ = Task.Run(async () =>
            {
                // 3. 手动创建一个新的 Scope
                using (var scope = _scopeFactory.CreateScope())
                {
                    // 4. 从新 Scope 里拿一个新的 DbContext
                    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    try
                    {
                        // 执行耗时的区块链操作
                        string txHash = await _blockchainService.RegisterArtifactAsync(currentFileHash);

                        // 使用新的 dbContext 更新数据库
                        var artifactToUpdate = await dbContext.Artifacts.FindAsync(Guid.Parse(currentArtifactId));
                        if (artifactToUpdate != null)
                        {
                            artifactToUpdate.Status = "Registered";
                            artifactToUpdate.TxHash = txHash; // 确保数据库已有此字段，否则注释掉
                            await dbContext.SaveChangesAsync();
                        }

                        // 推送成功消息（针对用户：用 Clients.User(currentUserId)）
                        await _hubContext.Clients.User(currentUserId).SendAsync("ReceiveMessage", "System",
                            $"✅ 上链成功！文件 '{currentTitle}' 已被锚定。TxHash: {txHash}", currentArtifactId);
                    }
                    catch (Exception ex)
                    {
                        // 失败处理：也要用新的 dbContext
                        var artifactToUpdate = await dbContext.Artifacts.FindAsync(Guid.Parse(currentArtifactId));
                        if (artifactToUpdate != null)
                        {
                            artifactToUpdate.Status = "Failed";
                            await dbContext.SaveChangesAsync();
                        }

                        // 推送失败消息（针对用户）
                        await _hubContext.Clients.User(currentUserId).SendAsync("ReceiveMessage", "System",
                            $"❌ 上链失败：{ex.Message}", currentArtifactId);
                    }
                } // 5. Scope 结束，dbContext 自动释放
            });

            return Accepted(new
            {
                message = "File accepted. Uploading to Blockchain in background...",
                artifactId = artifact.ArtifactId,
                hash = fileHash,
                status = "Pending"
            });
        }

        [HttpGet("verify/{fileHash}")]
        public async Task<IActionResult> Verify(string fileHash)
        {
            if (string.IsNullOrEmpty(fileHash)) return BadRequest("Hash is required.");

            var result = await _blockchainService.VerifyArtifactAsync(fileHash);
            if (result.Registered)
            {
                var verifyTime = DateTimeOffset.FromUnixTimeSeconds(result.Timestamp).DateTime.ToLocalTime();
                return Ok(new
                {
                    status = "Verified ✅",
                    message = "The document is authentically anchored on the Blockchain.",
                    details = new
                    {
                        ownerAddress = result.Owner,
                        registeredAt = verifyTime,
                        hash = fileHash
                    }
                });
            }
            else
            {
                return NotFound(new
                {
                    status = "Unverified ❌",
                    message = "This hash does not exist on the Smart Contract."
                });
            }
        }

        [Authorize]  // 加认证，确保只能下载自己的
        [HttpGet("download/{id}")]
        public async Task<IActionResult> Download(Guid id)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdString, out Guid ownerId))
            {
                return BadRequest("Invalid user ID from token.");
            }

            var artifact = await _context.Artifacts.FirstOrDefaultAsync(a => a.ArtifactId == id && a.OwnerId == ownerId);
            if (artifact == null) return NotFound("File record not found or not owned by you.");

            if (!System.IO.File.Exists(artifact.FilePath))
                return NotFound("Physical file is missing on server.");

            var memory = new MemoryStream();
            using (var stream = new FileStream(artifact.FilePath, FileMode.Open))
            {
                await stream.CopyToAsync(memory);
            }
            memory.Position = 0;
            return File(memory, "application/octet-stream", Path.GetFileName(artifact.FilePath));
        }

        // 新增：获取用户上传历史 API（前端 History 用这个替换 localStorage）
        [Authorize]
        [HttpGet("history")]
        public async Task<IActionResult> GetHistory()
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdString, out Guid ownerId))
            {
                return BadRequest("Invalid user ID from token.");
            }

            var artifacts = await _context.Artifacts
                .Where(a => a.OwnerId == ownerId)
                .Select(a => new
                {
                    a.ArtifactId,
                    a.Title,
                    a.Status,
                    a.UploadDate,
                    a.FileHash,
                    a.TxHash
                })
                .OrderByDescending(a => a.UploadDate)
                .ToListAsync();

            return Ok(artifacts);
        }
    }
}