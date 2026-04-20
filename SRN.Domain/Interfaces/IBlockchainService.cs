namespace SRN.Domain.Interfaces
{
    /// <summary>
    /// Defines the contract for external Web3/Blockchain interactions.
    /// By keeping this in the Domain layer, the core application doesn't need to know 
    /// if the underlying infrastructure uses Ethereum, Polygon, or Hyperledger.
    /// </summary>
    public interface IBlockchainService
    {
        /// <summary>
        /// Executes a smart contract transaction to permanently record a document's hash on the blockchain.
        /// Returns the resulting Transaction Hash (TxHash).
        /// </summary>
        Task<string> RegisterArtifactAsync(string fileHash);

        /// <summary>
        /// Queries the smart contract (read-only operation) to verify if a document hash exists.
        /// Returns a tuple containing the registration status, the wallet address of the owner, and the block timestamp.
        /// </summary>
        Task<(bool Registered, string Owner, long Timestamp)> VerifyArtifactAsync(string fileHash);
    }
}