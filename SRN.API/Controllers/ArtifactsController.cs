using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SRN.Application.DTOs;
using SRN.Application.Interfaces;
using System.Security.Claims;

namespace SRN.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ArtifactsController : ControllerBase
    {
        private readonly IArtifactService _artifactService;

        public ArtifactsController(IArtifactService artifactService)
        {
            _artifactService = artifactService;
        }

        [Authorize]
        [HttpPost("upload")]
        public async Task<IActionResult> Upload([FromForm] ArtifactUploadDto uploadDto)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return BadRequest("Invalid user ID from token.");

            var result = await _artifactService.UploadArtifactAsync(uploadDto, userId);

            if (!result.Success)
            {
                if (result.Message == "Artifact already exists")
                {
                    return Conflict(new { message = result.Message, artifactId = result.ArtifactId, fileHash = result.Hash });
                }
                return BadRequest(new { message = result.Message });
            }

            return Accepted(new { message = "File accepted. Processing...", artifactId = result.ArtifactId, status = "Pending" });
        }

        [HttpGet("verify/{fileHash}")]
        public async Task<IActionResult> Verify(string fileHash)
        {
            if (string.IsNullOrEmpty(fileHash)) return BadRequest("Hash is required.");

            var result = await _artifactService.VerifyArtifactAsync(fileHash);

            if (result.Registered)
            {
                return Ok(new { status = "Verified ✅", details = new { ownerAddress = result.Owner, registeredAt = result.RegisteredAt } });
            }
            return NotFound(new { status = "Unverified ❌" });
        }

        [Authorize]
        [HttpGet("download/{id}")]
        public async Task<IActionResult> Download(Guid id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return BadRequest("Invalid user ID.");

            var isAdmin = User.IsInRole("Admin");

            var artifact = await _artifactService.GetArtifactForDownloadAsync(id, userId, isAdmin);

            if (artifact == null) return NotFound("Not found or access denied.");

            var absolutePath = Path.Combine(Directory.GetCurrentDirectory(), artifact.FilePath);

            if (!System.IO.File.Exists(absolutePath)) return NotFound("File missing.");

            var memory = new MemoryStream();
            using (var stream = new FileStream(absolutePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                await stream.CopyToAsync(memory);
            }
            memory.Position = 0;
            return File(memory, "application/octet-stream", Path.GetFileName(absolutePath));
        }

        [Authorize]
        [HttpGet("history")]
        public async Task<IActionResult> GetHistory()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return BadRequest("Invalid user ID.");

            var history = await _artifactService.GetHistoryAsync(userId);
            return Ok(history);
        }

        [Authorize]
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return BadRequest("Invalid user ID.");

            var success = await _artifactService.DeleteArtifactAsync(id, userId);

            if (!success) return NotFound(new { message = "Artifact not found or access denied." });

            return Ok(new { message = "Artifact deleted successfully." });
        }

        [Authorize(Roles = "Admin")]
        [HttpPost("{id}/approve")]
        public async Task<IActionResult> ApproveArtifact(Guid id)
        {
            var result = await _artifactService.ApproveAndRegisterArtifactAsync(id);

            if (!result.Success)
            {
                return BadRequest(new { message = result.Message });
            }

            return Accepted(new { message = result.Message });
        }

        [Authorize(Roles = "Admin")]
        [HttpGet("all")]
        public async Task<IActionResult> GetAllArtifactsForAdmin()
        {
            var allArtifacts = await _artifactService.GetAllArtifactsForAdminAsync();
            return Ok(allArtifacts);
        }

        [HttpGet("public")]
        public async Task<IActionResult> GetPublicArtifacts()
        {
            var publicArtifacts = await _artifactService.GetPublicArtifactsAsync();
            return Ok(publicArtifacts);
        }

        [HttpGet("public/download/{id}")]
        public async Task<IActionResult> PublicDownload(Guid id)
        {
            var artifact = await _artifactService.GetPublicArtifactForDownloadAsync(id);

            if (artifact == null) return NotFound("Artifact not found or not yet published.");

            var absolutePath = Path.Combine(Directory.GetCurrentDirectory(), artifact.FilePath);

            if (!System.IO.File.Exists(absolutePath)) return NotFound("File missing on server.");

            var memory = new MemoryStream();
            using (var stream = new FileStream(absolutePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                await stream.CopyToAsync(memory);
            }
            memory.Position = 0;

            return File(memory, "application/octet-stream", Path.GetFileName(absolutePath));
        }
    }
}