using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SRN.Domain.Entities;
using SRN.Infrastructure.Persistence;
using SRN.Infrastructure.Blockchain;
using System.Security.Cryptography;
using SRN.API.DTOs;
using System.Security.Claims;

namespace SRN.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ArtifactsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IBlockchainService _blockchainService;

        public ArtifactsController(ApplicationDbContext context, IBlockchainService blockchainService)
        {
            _context = context;
            _blockchainService = blockchainService;
        }

        [Authorize]
        [HttpPost("upload")]
        public async Task<IActionResult> Upload([FromForm] ArtifactUploadDto uploadDto)
        {
            if (uploadDto.File == null || uploadDto.File.Length == 0)
                return BadRequest("No file uploaded.");

            string fileHash;

            using (var sha256 = SHA256.Create())
            {
                using (var stream = uploadDto.File.OpenReadStream())
                {
                    var hashBytes = await sha256.ComputeHashAsync(stream);
                    fileHash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
                }
            }

            var existingArtifact = _context.Artifacts.FirstOrDefault(a => a.FileHash == fileHash);
            if (existingArtifact != null)
            {
                return Conflict(new { message = "Artifact already exists", artifactId = existingArtifact.ArtifactId });
            }

            var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "Uploads");
            if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

            var filePath = Path.Combine(uploadsFolder, $"{Guid.NewGuid()}_{uploadDto.File.FileName}");

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await uploadDto.File.CopyToAsync(stream);
            }

            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            Guid ownerId = Guid.TryParse(userIdString, out var parsedId) ? parsedId : Guid.NewGuid();

            var artifact = new Artifact
            {
                ArtifactId = Guid.NewGuid(),
                Title = uploadDto.Title,
                FileHash = fileHash,
                FilePath = filePath,
                UploadDate = DateTime.UtcNow,
                Status = "Pending Blockchain",
                OwnerId = ownerId
            };

            _context.Artifacts.Add(artifact);
            await _context.SaveChangesAsync();

            string txHash = await _blockchainService.RegisterArtifactAsync(fileHash);

            artifact.Status = "Verified On-Chain";
            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Upload & Anchoring Successful",
                artifactId = artifact.ArtifactId,
                hash = fileHash,
                blockchainTx = txHash,
                ownerId = ownerId
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