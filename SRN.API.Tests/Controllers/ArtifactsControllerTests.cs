using Microsoft.AspNetCore.Mvc;
using Moq;
using SRN.API.Controllers;
using SRN.Application.Interfaces;

namespace SRN.API.Tests.Controllers
{
    public class ArtifactsControllerTests
    {
        private readonly Mock<IArtifactService> _mockArtifactService;
        private readonly ArtifactsController _controller;

        public ArtifactsControllerTests()
        {
            _mockArtifactService = new Mock<IArtifactService>();

            _controller = new ArtifactsController(_mockArtifactService.Object);
        }

        [Fact]
        public async Task Verify_WithValidHash_ReturnsOkResult()
        {
            var fakeHash = "abc123hash";
            var fakeDate = DateTime.UtcNow;

            _mockArtifactService
                .Setup(s => s.VerifyArtifactAsync(fakeHash))
                .ReturnsAsync((Registered: true, Owner: "0xFakeWallet", RegisteredAt: fakeDate));

            var result = await _controller.Verify(fakeHash);

            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.Contains("Verified", okResult.Value?.ToString() ?? "");
        }

        [Fact]
        public async Task Verify_WithUnregisteredHash_ReturnsNotFound()
        {
            var fakeHash = "unknownHash";

            _mockArtifactService
                .Setup(s => s.VerifyArtifactAsync(fakeHash))
                .ReturnsAsync((Registered: false, Owner: string.Empty, RegisteredAt: null));

            var result = await _controller.Verify(fakeHash);

            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
            Assert.Contains("Unverified", notFoundResult.Value?.ToString() ?? "");
        }
    }
}