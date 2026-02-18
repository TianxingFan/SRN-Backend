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
        private readonly ApplicationDbContext _context;
        private readonly IBlockchainService _blockchainService;
        private readonly IHubContext<NotificationHub> _hubContext;
        private readonly IServiceScopeFactory _scopeFactory;

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

        [Authorize]
        [HttpPost("upload")]
        public async Task<IActionResult> Upload([FromForm] ArtifactUploadDto uploadDto)
        {
            // FluentValidation handles null checks automatically

            // --- 1. Compute Hash ---
            string fileHash;
            using (var sha256 = SHA256.Create())
            {
                using (var stream = uploadDto.File.OpenReadStream())
                {
                    var hashBytes = await sha256.ComputeHashAsync(stream);
                    fileHash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
                }
            }

            // --- 2. Deduplication ---
            var existingArtifact = _context.Artifacts.FirstOrDefault(a => a.FileHash == fileHash);
            if (existingArtifact != null)
            {
                return Conflict(new { message = "Artifact already exists", artifactId = existingArtifact.ArtifactId });
            }

            // --- 3. Save File ---
            var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "Uploads");
            if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);
            var filePath = Path.Combine(uploadsFolder, $"{Guid.NewGuid()}_{uploadDto.File.FileName}");
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await uploadDto.File.CopyToAsync(stream);
            }

            // --- 4. Save to Database ---
            // Fix: Use string for UserId directly
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
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
                OwnerId = userId // Assign string directly
            };

            _context.Artifacts.Add(artifact);
            await _context.SaveChangesAsync();

            // Prepare variables for background task
            var currentArtifactId = artifact.ArtifactId.ToString();
            var currentFileHash = fileHash;
            var currentTitle = uploadDto.Title;
            var currentUserId = userId; // string

            // --- 5. Background Task ---
            _ = Task.Run(async () =>
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    try
                    {
                        string txHash = await _blockchainService.RegisterArtifactAsync(currentFileHash);

                        var artifactToUpdate = await dbContext.Artifacts.FindAsync(Guid.Parse(currentArtifactId));
                        if (artifactToUpdate != null)
                        {
                            artifactToUpdate.Status = "Registered";
                            artifactToUpdate.TxHash = txHash;
                            await dbContext.SaveChangesAsync();
                        }

                        // Push Success
                        await _hubContext.Clients.Group(currentUserId).SendAsync("ReceiveMessage", "System",
                            $"✅ 上链成功！TxHash: {txHash}", currentArtifactId);
                    }
                    catch (Exception ex)
                    {
                        var artifactToUpdate = await dbContext.Artifacts.FindAsync(Guid.Parse(currentArtifactId));
                        if (artifactToUpdate != null)
                        {
                            artifactToUpdate.Status = "Failed";
                            await dbContext.SaveChangesAsync();
                        }
                        // Push Failure
                        await _hubContext.Clients.Group(currentUserId).SendAsync("ReceiveMessage", "System",
                            $"❌ 上链失败：{ex.Message}", currentArtifactId);
                    }
                }
            });

            return Accepted(new
            {
                message = "File accepted. Processing...",
                artifactId = artifact.ArtifactId,
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
                return Ok(new { status = "Verified ✅", details = new { ownerAddress = result.Owner, registeredAt = verifyTime } });
            }
            return NotFound(new { status = "Unverified ❌" });
        }

        [Authorize]
        [HttpGet("download/{id}")]
        public async Task<IActionResult> Download(Guid id)
        {
            // Fix: Use string for UserId comparison
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return BadRequest("Invalid user ID.");

            // Compare OwnerId (string) with userId (string)
            var artifact = await _context.Artifacts.FirstOrDefaultAsync(a => a.ArtifactId == id && a.OwnerId == userId);

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
            // Fix: Use string for UserId comparison
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return BadRequest("Invalid user ID.");

            var artifacts = await _context.Artifacts
                .Where(a => a.OwnerId == userId) // Compare string to string
                .Select(a => new { a.ArtifactId, a.Title, a.Status, a.UploadDate, a.FileHash, a.TxHash })
                .OrderByDescending(a => a.UploadDate)
                .ToListAsync();

            return Ok(artifacts);
        }
    }
}