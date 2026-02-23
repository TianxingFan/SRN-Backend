using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Moq;
using SRN.API.Controllers;
using SRN.API.Hubs;
using SRN.Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace SRN.API.Tests.Controllers
{
    public class ArtifactsControllerTests
    {
        private readonly Mock<IArtifactRepository> _mockRepo;
        private readonly Mock<IBlockchainService> _mockBlockchain;
        private readonly Mock<IHubContext<NotificationHub>> _mockHub;
        private readonly Mock<IServiceScopeFactory> _mockScopeFactory;
        private readonly ArtifactsController _controller;

        public ArtifactsControllerTests()
        {
            _mockRepo = new Mock<IArtifactRepository>();
            _mockBlockchain = new Mock<IBlockchainService>();
            _mockHub = new Mock<IHubContext<NotificationHub>>();
            _mockScopeFactory = new Mock<IServiceScopeFactory>();

            _controller = new ArtifactsController(
                _mockRepo.Object,
                _mockBlockchain.Object,
                _mockHub.Object,
                _mockScopeFactory.Object
            );
        }

        [Fact]
        public async Task Verify_WithValidHash_ReturnsOkResult()
        {
            // Arrange
            var fakeHash = "abc123hash";

            // ⚠️ 修复点：严格按照 (bool Registered, string Owner, long Timestamp) 的顺序，并且数字后面加 L 强制转为 long
            _mockBlockchain
                .Setup(b => b.VerifyArtifactAsync(fakeHash))
                .ReturnsAsync((Registered: true, Owner: "0xFakeWallet", Timestamp: 1620000000L));

            // Act
            var result = await _controller.Verify(fakeHash);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.Contains("Verified", okResult.Value?.ToString() ?? "");
        }

        [Fact]
        public async Task Verify_WithUnregisteredHash_ReturnsNotFound()
        {
            // Arrange
            var fakeHash = "unknownHash";

            // ⚠️ 修复点：同上，严格匹配顺序和类型
            _mockBlockchain
                .Setup(b => b.VerifyArtifactAsync(fakeHash))
                .ReturnsAsync((Registered: false, Owner: "", Timestamp: 0L));

            // Act
            var result = await _controller.Verify(fakeHash);

            // Assert
            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
            Assert.Contains("Unverified", notFoundResult.Value?.ToString() ?? "");
        }
    }
}