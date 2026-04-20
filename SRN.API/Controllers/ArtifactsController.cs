using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SRN.Application.DTOs;
using SRN.Application.Interfaces;
using System.Security.Claims;

namespace SRN.API.Controllers
{
    /// <summary>
    /// Handles all HTTP requests related to research artifacts (documents), 
    /// including file uploads, downloads, admin approvals, and blockchain verifications.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class ArtifactsController : ControllerBase
    {
        private readonly IArtifactService _artifactService;

        // Inject the application service layer to keep the controller lean and focused on HTTP routing
        public ArtifactsController(IArtifactService artifactService)
        {
            _artifactService = artifactService;
        }

        /// <summary>
        /// Authenticated endpoint for researchers to upload new PDF documents.
        /// </summary>
        [Authorize]
        [HttpPost("upload")]
        public async Task<IActionResult> Upload([FromForm] ArtifactUploadDto uploadDto)
        {
            // Extract the user's unique identifier securely from the JWT payload
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return BadRequest("Invalid user ID from token.");

            var result = await _artifactService.UploadArtifactAsync(uploadDto, userId);

            if (!result.Success)
            {
                // Return a 409 Conflict if the exact same file hash already exists in the system
                if (result.Message == "Artifact already exists")
                {
                    return Conflict(new { message = result.Message, artifactId = result.ArtifactId, fileHash = result.Hash });
                }
                return BadRequest(new { message = result.Message });
            }

            // Return 202 Accepted to indicate the file was received and is pending admin review
            return Accepted(new { message = "File accepted. Processing...", artifactId = result.ArtifactId, status = "Pending" });
        }

        /// <summary>
        /// Public endpoint to verify a document's cryptographic integrity against the blockchain.
        /// </summary>
        [HttpGet("verify/{fileHash}")]
        public async Task<IActionResult> Verify(string fileHash)
        {
            if (string.IsNullOrEmpty(fileHash)) return BadRequest("Hash is required.");

            // Query the Ethereum smart contract via the service layer
            var result = await _artifactService.VerifyArtifactAsync(fileHash);

            if (result.Registered)
            {
                return Ok(new { status = "Verified ✅", details = new { ownerAddress = result.Owner, registeredAt = result.RegisteredAt } });
            }
            return NotFound(new { status = "Unverified ❌" });
        }

        /// <summary>
        /// Authenticated endpoint for users to download their own private or pending documents.
        /// </summary>
        [Authorize]
        [HttpGet("download/{id}")]
        public async Task<IActionResult> Download(Guid id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return BadRequest("Invalid user ID.");

            // Check if the requester is an Admin, which grants global read access
            var isAdmin = User.IsInRole("Admin");

            var artifact = await _artifactService.GetArtifactForDownloadAsync(id, userId, isAdmin);
            if (artifact == null) return NotFound("Not found or access denied.");

            var absolutePath = Path.Combine(Directory.GetCurrentDirectory(), artifact.FilePath);
            if (!System.IO.File.Exists(absolutePath)) return NotFound("File missing.");

            // Load the file into memory and return it as a binary stream to the client
            var memory = new MemoryStream();
            using (var stream = new FileStream(absolutePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                await stream.CopyToAsync(memory);
            }
            memory.Position = 0;
            return File(memory, "application/octet-stream", Path.GetFileName(absolutePath));
        }

        /// <summary>
        /// Retrieves the personal upload history for the currently authenticated user.
        /// </summary>
        [Authorize]
        [HttpGet("history")]
        public async Task<IActionResult> GetHistory()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return BadRequest("Invalid user ID.");

            var history = await _artifactService.GetHistoryAsync(userId);
            return Ok(history);
        }

        /// <summary>
        /// Allows users to delete their own unapproved documents, or admins to delete any document.
        /// </summary>
        [Authorize]
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return BadRequest("Invalid user ID.");

            var isAdmin = User.IsInRole("Admin");
            var success = await _artifactService.DeleteArtifactAsync(id, userId, isAdmin);

            if (!success) return NotFound(new { message = "Artifact not found or access denied." });

            return Ok(new { message = "Artifact deleted successfully." });
        }

        /// <summary>
        /// Admin-only endpoint to approve a pending document and initiate the blockchain anchoring process.
        /// </summary>
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

        /// <summary>
        /// Admin-only endpoint to retrieve all documents across the system for the management panel.
        /// </summary>
        [Authorize(Roles = "Admin")]
        [HttpGet("all")]
        public async Task<IActionResult> GetAllArtifactsForAdmin()
        {
            var allArtifacts = await _artifactService.GetAllArtifactsForAdminAsync();
            return Ok(allArtifacts);
        }

        /// <summary>
        /// Public endpoint to fetch metadata for all successfully published (blockchain-registered) documents.
        /// </summary>
        [HttpGet("public")]
        public async Task<IActionResult> GetPublicArtifacts()
        {
            var publicArtifacts = await _artifactService.GetPublicArtifactsAsync();
            return Ok(publicArtifacts);
        }

        /// <summary>
        /// Public endpoint to download published documents.
        /// Incorporates an on-demand security check to detect physical file tampering on the server.
        /// </summary>
        [HttpGet("public/download/{id}")]
        public async Task<IActionResult> PublicDownload(Guid id, [FromQuery] bool forceDownload = false)
        {
            // The service dynamically recalculates the SHA-256 hash of the local file
            var (isTampered, artifact) = await _artifactService.GetPublicArtifactForDownloadAsync(id);

            if (artifact == null) return NotFound("Artifact not found or not yet published.");

            // Intercept the download if the file hash doesn't match the database/blockchain record,
            // unless the user has explicitly acknowledged the risk (forceDownload = true)
            if (isTampered && !forceDownload)
            {
                return BadRequest(new
                {
                    isTampered = true,
                    message = "Warning: The file doesn’t match the original. Download anyway?"
                });
            }

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