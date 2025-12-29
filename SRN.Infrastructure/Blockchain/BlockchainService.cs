using Microsoft.Extensions.Configuration;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;
using Nethereum.Hex.HexTypes;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Web3;
using Nethereum.Web3.Accounts;
using System.IO;
using System.Threading.Tasks;

namespace SRN.Infrastructure.Blockchain
{
    public class BlockchainService
    {
        private readonly Web3 _web3;
        private readonly string _contractAddress;
        private readonly string _abi;

        public BlockchainService(IConfiguration configuration)
        {
            // 1. Load credentials and network settings from Configuration (e.g., User Secrets or appsettings.json)
            var privateKey = configuration["Blockchain:PrivateKey"];
            var rpcUrl = configuration["Blockchain:RpcUrl"];
            _contractAddress = configuration["Blockchain:ContractAddress"];

            // 2. Initialize the Web3 connection with the account
            var account = new Account(privateKey);
            _web3 = new Web3(account, rpcUrl);

            // 3. Load the ABI JSON file
            // Note: Ensure the JSON file property "Copy to Output Directory" is set to "Copy if newer"
            var abiPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Blockchain", "ArtifactRegistryABI.json");
            if (File.Exists(abiPath))
            {
                _abi = File.ReadAllText(abiPath);
            }
            else
            {
                throw new FileNotFoundException("ABI file not found. Please check the file path and build settings.");
            }
        }

        public async Task<string> RegisterArtifactAsync(string fileHash)
        {
            var contract = _web3.Eth.GetContract(_abi, _contractAddress);
            var registerFunction = contract.GetFunction("registerArtifact");

            // 1. Convert the Hex string to a Byte Array (required for Solidity bytes32)
            var hashBytes = fileHash.HexToByteArray();

            // 2. Define a fixed Gas Limit
            var fixedGas = new HexBigInteger(400000);

            // 3. Sign and Send the Transaction
            var receipt = await registerFunction.SendTransactionAsync(
                _web3.TransactionManager.Account.Address,
                fixedGas, // Gas Limit
                new HexBigInteger(0), // Value (0 ETH sent)
                hashBytes // Function parameter
            );

            return receipt;
        }

        public async Task<(bool Registered, string Owner, long Timestamp)> VerifyArtifactAsync(string fileHash)
        {
            var contract = _web3.Eth.GetContract(_abi, _contractAddress);
            var verifyFunction = contract.GetFunction("verifyArtifact");

            // Ensure the input string is converted to bytes for the contract call
            var hashBytes = fileHash.HexToByteArray();

            // Call the Smart Contract (Read-only, no Gas cost) and deserialize the result
            var result = await verifyFunction.CallDeserializingToObjectAsync<VerifyResult>(hashBytes);

            return (result.Registered, result.Owner, result.Timestamp);
        }

        // Helper DTO to map the return values from the 'verifyArtifact' Solidity function
        [FunctionOutput]
        public class VerifyResult : IFunctionOutputDTO
        {
            [Parameter("bool", "registered", 1)]
            public bool Registered { get; set; }

            [Parameter("uint256", "timestamp", 2)]
            public long Timestamp { get; set; }

            [Parameter("address", "owner", 3)]
            public string Owner { get; set; }
        }
    }
}