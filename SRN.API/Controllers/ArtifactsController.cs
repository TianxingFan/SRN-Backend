using Microsoft.AspNetCore.Mvc;
using SRN.Infrastructure.Persistence;
using SRN.Infrastructure.Blockchain;
using System.Security.Cryptography;
using SRN.API.DTOs;
using Microsoft.AspNetCore.SignalR;
using SRN.API.Hubs;
using Microsoft.Extensions.DependencyInjection; // 必须引用这个！

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

        // [Authorize] // 测试时暂时注释
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
            // ⚠️ 确保这个 ID 在你的数据库 AspNetUsers 表里存在，否则报错
            Guid ownerId = Guid.Parse("11111111-1111-1111-1111-111111111111");

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

            // 保存这就变量，供后台任务使用
            var currentArtifactId = artifact.ArtifactId;
            var currentFileHash = fileHash;
            var currentTitle = uploadDto.Title;

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
                        var artifactToUpdate = await dbContext.Artifacts.FindAsync(currentArtifactId);
                        if (artifactToUpdate != null)
                        {
                            artifactToUpdate.Status = "Registered";
                            artifactToUpdate.TxHash = txHash; // 确保数据库已有此字段，否则注释掉
                            await dbContext.SaveChangesAsync();
                        }

                        // 推送成功消息
                        await _hubContext.Clients.All.SendAsync("ReceiveMessage", "System",
                            $"✅ 上链成功！文件 '{currentTitle}' 已被锚定。TxHash: {txHash}", currentArtifactId.ToString());
                    }
                    catch (Exception ex)
                    {
                        // 失败处理：也要用新的 dbContext
                        var artifactToUpdate = await dbContext.Artifacts.FindAsync(currentArtifactId);
                        if (artifactToUpdate != null)
                        {
                            artifactToUpdate.Status = "Failed";
                            await dbContext.SaveChangesAsync();
                        }

                        // 推送失败消息
                        await _hubContext.Clients.All.SendAsync("ReceiveMessage", "System",
                            $"❌ 上链失败：{ex.Message}", currentArtifactId.ToString());
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

        [HttpGet("download/{id}")]
        public async Task<IActionResult> Download(Guid id)
        {
            var artifact = await _context.Artifacts.FindAsync(id);
            if (artifact == null) return NotFound("File record not found.");

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
    }
}