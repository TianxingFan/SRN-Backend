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
    public class EthereumBlockchainService : IBlockchainService
    {
        private readonly Web3 _web3;
        private readonly string _contractAddress;
        private readonly string _abi;

        public EthereumBlockchainService(IConfiguration configuration)
        {
            var privateKey = configuration["Blockchain:PrivateKey"];
            var rpcUrl = configuration["Blockchain:RpcUrl"];
            _contractAddress = configuration["Blockchain:ContractAddress"];

            var account = new Account(privateKey);
            _web3 = new Web3(account, rpcUrl);

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

        public async Task<string> RegisterArtifactAsync(string fileHash)
        {
            var contract = _web3.Eth.GetContract(_abi, _contractAddress);
            var registerFunction = contract.GetFunction("registerArtifact");

            var hashBytes = fileHash.HexToByteArray();
            var fixedGas = new HexBigInteger(400000);

            var receipt = await registerFunction.SendTransactionAsync(
                _web3.TransactionManager.Account.Address,
                fixedGas,
                new HexBigInteger(0),
                hashBytes
            );

            return receipt;
        }

        public async Task<(bool Registered, string Owner, long Timestamp)> VerifyArtifactAsync(string fileHash)
        {
            var contract = _web3.Eth.GetContract(_abi, _contractAddress);
            var verifyFunction = contract.GetFunction("verifyArtifact");

            var hashBytes = fileHash.HexToByteArray();

            var result = await verifyFunction.CallDeserializingToObjectAsync<VerifyResult>(hashBytes);

            return (result.Registered, result.Owner, result.Timestamp);
        }

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