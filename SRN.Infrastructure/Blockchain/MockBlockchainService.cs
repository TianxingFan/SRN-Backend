using System.Threading.Tasks;
using System;

namespace SRN.Infrastructure.Blockchain
{
    public class MockBlockchainService : IBlockchainService
    {
        public Task<string> RegisterArtifactAsync(string fileHash)
        {
            var fakeTxHash = "0xMOCK_TX_" + Guid.NewGuid().ToString().Replace("-", "");
            return Task.FromResult(fakeTxHash);
        }
        public Task<(bool Registered, string Owner, long Timestamp)> VerifyArtifactAsync(string fileHash)
        {
            var fakeOwner = "0xMOCK_OWNER_ADDRESS";
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            return Task.FromResult((true, fakeOwner, timestamp));
        }
    }
}