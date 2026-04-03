pragma solidity ^0.8.20;

interface IArtifactRegistry {
    
    struct Record {
        bytes32 documentHash;
        address owner;
        uint256 timestamp;
        bool isRegistered;
    }

    event DocumentAnchored(
        bytes32 indexed docHash,
        address indexed owner,
        uint256 timestamp
    );

    function registerArtifact(bytes32 _hash) external;

    function verifyArtifact(bytes32 _hash) external view returns (
        bool registered,
        uint256 timestamp,
        address owner
    );
}

contract ArtifactRegistry is IArtifactRegistry {
    mapping(bytes32 => Record) private registry;

    function registerArtifact(bytes32 _hash) external override {
        require(!registry[_hash].isRegistered, "Artifact already registered");

        registry[_hash] = Record({
            documentHash: _hash,
            owner: msg.sender,
            timestamp: block.timestamp,
            isRegistered: true
        });

        emit DocumentAnchored(_hash, msg.sender, block.timestamp);
    }

    function verifyArtifact(bytes32 _hash) external view override returns (
        bool registered,
        uint256 timestamp,
        address owner
    ) {
        Record memory record = registry[_hash];
        return (record.isRegistered, record.timestamp, record.owner);
    }
}