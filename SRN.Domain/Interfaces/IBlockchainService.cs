namespace SRN.Domain.Interfaces
{
    public interface IBlockchainService
    {
        Task<string> RegisterArtifactAsync(string fileHash);

        Task<(bool Registered, string Owner, long Timestamp)> VerifyArtifactAsync(string fileHash);
    }
}