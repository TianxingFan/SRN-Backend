using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SRN.API.DTOs;
using SRN.API.Hubs;
using SRN.Domain.Entities;
using SRN.Infrastructure.Blockchain;
using SRN.Infrastructure.Persistence;
using System.Security.Claims;
using System.Security.Cryptography;

namespace SRN.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ArtifactsController : ControllerBase
    {
        private readonly ApplicationDbContext _context; // 仅供同步代码使用
        private readonly IBlockchainService _blockchainService;
        private readonly IHubContext<NotificationHub> _hubContext;
        private readonly IServiceScopeFactory _scopeFactory; // 1. 工厂用于后台任务

        public ArtifactsController(
            ApplicationDbContext context,
            IBlockchainService blockchainService,
            IHubContext<NotificationHub> hubContext,
            IServiceScopeFactory scopeFactory)
        {
            _context = context;
            _blockchainService = blockchainService;
            _hubContext = hubContext;
            _scopeFactory = scopeFactory;
        }

        [Authorize] // 🔒 生产模式：开启认证
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

            // --- 2. 查重 ---
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
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier); // 从 JWT 获取用户ID
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
                // TxHash 等待上链后更新
            };

            _context.Artifacts.Add(artifact);
            await _context.SaveChangesAsync();

            // 保存变量供后台线程使用
            var currentArtifactId = artifact.ArtifactId.ToString();
            var currentFileHash = fileHash;
            var currentTitle = uploadDto.Title;
            var currentUserId = ownerId.ToString(); // 用于推送

            // --- 5. 启动后台任务 (使用 ScopeFactory 防止 Context 销毁) ---
            _ = Task.Run(async () =>
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    // 获取新的 DbContext 实例
                    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                    try
                    {
                        // 执行耗时上链操作
                        string txHash = await _blockchainService.RegisterArtifactAsync(currentFileHash);

                        // 更新数据库状态
                        var artifactToUpdate = await dbContext.Artifacts.FindAsync(Guid.Parse(currentArtifactId));
                        if (artifactToUpdate != null)
                        {
                            artifactToUpdate.Status = "Registered";
                            artifactToUpdate.TxHash = txHash;
                            await dbContext.SaveChangesAsync();
                        }

                        // ✅ 关键修改：使用 Clients.Group 配合 Hub 中的 Groups.AddToGroupAsync
                        await _hubContext.Clients.Group(currentUserId).SendAsync("ReceiveMessage", "System",
                            $"✅ 上链成功！文件 '{currentTitle}' 已被锚定。TxHash: {txHash}", currentArtifactId);
                    }
                    catch (Exception ex)
                    {
                        // 失败处理
                        var artifactToUpdate = await dbContext.Artifacts.FindAsync(Guid.Parse(currentArtifactId));
                        if (artifactToUpdate != null)
                        {
                            artifactToUpdate.Status = "Failed";
                            await dbContext.SaveChangesAsync();
                        }

                        // 推送失败消息
                        await _hubContext.Clients.Group(currentUserId).SendAsync("ReceiveMessage", "System",
                            $"❌ 上链失败：{ex.Message}", currentArtifactId);
                    }
                }
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

        [Authorize]
        [HttpGet("download/{id}")]
        public async Task<IActionResult> Download(Guid id)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdString, out Guid ownerId)) return BadRequest("Invalid user ID.");

            // 安全检查：只能下载属于自己的文件
            var artifact = await _context.Artifacts.FirstOrDefaultAsync(a => a.ArtifactId == id && a.OwnerId == ownerId);

            if (artifact == null) return NotFound("File record not found or access denied.");
            if (!System.IO.File.Exists(artifact.FilePath)) return NotFound("Physical file is missing.");

            var memory = new MemoryStream();
            using (var stream = new FileStream(artifact.FilePath, FileMode.Open))
            {
                await stream.CopyToAsync(memory);
            }
            memory.Position = 0;
            return File(memory, "application/octet-stream", Path.GetFileName(artifact.FilePath));
        }

        [Authorize] // 🔒 必须登录才能看历史
        [HttpGet("history")]
        public async Task<IActionResult> GetHistory()
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdString, out Guid ownerId)) return BadRequest("Invalid user ID.");

            // 只查询当前登录用户的记录
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