using Microsoft.Extensions.Configuration;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Hex.HexTypes;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Web3;
using Nethereum.Web3.Accounts;
using SRN.Domain.Interfaces;

namespace SRN.Infrastructure.Blockchain
{
    /// <summary>
    /// Implements the blockchain service using Nethereum to interact directly with 
    /// an Ethereum-compatible network (e.g., Sepolia Testnet or Mainnet).
    /// </summary>
    public class EthereumBlockchainService : IBlockchainService
    {
        private readonly Web3 _web3;
        private readonly string _contractAddress;
        private readonly string _abi;

        /// <summary>
        /// Initializes the Web3 instance using environment configuration securely injected at runtime.
        /// </summary>
        public EthereumBlockchainService(IConfiguration configuration)
        {
            // Securely load the hot wallet private key and RPC endpoint
            var privateKey = configuration["Blockchain:PrivateKey"] ?? throw new ArgumentNullException("Blockchain:PrivateKey is missing");
            var rpcUrl = configuration["Blockchain:RpcUrl"] ?? throw new ArgumentNullException("Blockchain:RpcUrl is missing");

            _contractAddress = configuration["Blockchain:ContractAddress"] ?? throw new ArgumentNullException("Blockchain:ContractAddress is missing");

            // Initialize an Ethereum Account object capable of signing transactions locally
            var account = new Account(privateKey);
            _web3 = new Web3(account, rpcUrl);

            // Load the Application Binary Interface (ABI) required to interact with the deployed Solidity Smart Contract
            var abiPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Blockchain", "ArtifactRegistryABI.json");
            if (File.Exists(abiPath))
            {
                _abi = File.ReadAllText(abiPath);
            }
            else
            {
                throw new FileNotFoundException("ABI file not found.");
            }
        }

        /// <summary>
        /// Submits a signed state-changing transaction to the Ethereum network to anchor the document's hash.
        /// </summary>
        public async Task<string> RegisterArtifactAsync(string fileHash)
        {
            var contract = _web3.Eth.GetContract(_abi, _contractAddress);
            var registerFunction = contract.GetFunction("registerArtifact");

            // Convert the standard hex string hash into a byte array compatible with Solidity's bytes32 type
            var hashBytes = fileHash.HexToByteArray();

            // Hardcode a safe gas limit to prevent transaction failure (Out of Gas)
            var fixedGas = new HexBigInteger(400000);

            // Execute the transaction. This mutates blockchain state and costs gas.
            var receipt = await registerFunction.SendTransactionAsync(
                _web3.TransactionManager.Account.Address,
                fixedGas,
                new HexBigInteger(0), // No native ETH value is sent with this transaction
                hashBytes
            );

            // Return the transaction hash (TxHash) so the system can track it on Etherscan
            return receipt;
        }

        /// <summary>
        /// Executes a read-only call to the Ethereum smart contract to verify if a hash exists.
        /// </summary>
        public async Task<(bool Registered, string Owner, long Timestamp)> VerifyArtifactAsync(string fileHash)
        {
            var contract = _web3.Eth.GetContract(_abi, _contractAddress);
            var verifyFunction = contract.GetFunction("verifyArtifact");

            var hashBytes = fileHash.HexToByteArray();

            // Perform a gas-less "call" to read data from the blockchain state
            var result = await verifyFunction.CallDeserializingToObjectAsync<VerifyResult>(hashBytes);

            return (result.Registered, result.Owner, result.Timestamp);
        }

        /// <summary>
        /// Nethereum DTO class mapping the multi-variable output returned by the Solidity smart contract.
        /// </summary>
        [FunctionOutput]
        public class VerifyResult : IFunctionOutputDTO
        {
            [Parameter("bool", "registered", 1)]
            public bool Registered { get; set; }

            // Maps Solidity's uint256 timestamp (Unix Epoch) to a C# long
            [Parameter("uint256", "timestamp", 2)]
            public long Timestamp { get; set; }

            [Parameter("address", "owner", 3)]
            public string Owner { get; set; } = string.Empty;
        }
    }
}