// SPDX-License-Identifier: MIT
pragma solidity ^0.8.20;

/// @title Interface for the SRN Artifact Registry
/// @notice Defines the standard functions and events for recording document hashes on the blockchain.
interface IArtifactRegistry {
    
    /// @notice Represents a single document record on the blockchain.
    struct Record {
        bytes32 documentHash; // The SHA-256 hash of the physical document
        address owner;        // The wallet address of the user who registered it
        uint256 timestamp;    // The block timestamp when it was registered
        bool isRegistered;    // Flag to check if the record exists
    }

    /// @notice Emitted when a new document is successfully anchored to the blockchain.
    /// @param docHash The unique SHA-256 hash of the document.
    /// @param owner The wallet address that initiated the registration.
    /// @param timestamp The exact time the block was mined.
    event DocumentAnchored(
        bytes32 indexed docHash,
        address indexed owner,
        uint256 timestamp
    );

    /// @notice Registers a new document hash into the immutable ledger.
    /// @param _hash The bytes32 hash of the document to anchor.
    function registerArtifact(bytes32 _hash) external;

    /// @notice Verifies if a document hash exists in the registry.
    /// @param _hash The bytes32 hash of the document to verify.
    /// @return registered Boolean indicating if it exists.
    /// @return timestamp The time it was anchored (0 if not found).
    /// @return owner The address of the original publisher.
    function verifyArtifact(bytes32 _hash) external view returns (
        bool registered,
        uint256 timestamp,
        address owner
    );
}

/// @title SRN Artifact Registry Smart Contract
/// @notice Implements the IArtifactRegistry interface to provide an immutable, decentralized ledger for academic papers.
contract ArtifactRegistry is IArtifactRegistry {
    
    // Maps a document hash to its immutable Record details
    mapping(bytes32 => Record) private registry;

    /// @inheritdoc IArtifactRegistry
    function registerArtifact(bytes32 _hash) external override {
        // Security check: Prevent overwriting an existing document record to maintain absolute immutability
        require(!registry[_hash].isRegistered, "Artifact already registered");

        // Store the new record in the mapping
        registry[_hash] = Record({
            documentHash: _hash,
            owner: msg.sender,
            timestamp: block.timestamp,
            isRegistered: true
        });

        // Trigger the event so external systems (like the C# backend) can listen for successful anchors
        emit DocumentAnchored(_hash, msg.sender, block.timestamp);
    }

    /// @inheritdoc IArtifactRegistry
    function verifyArtifact(bytes32 _hash) external view override returns (
        bool registered,
        uint256 timestamp,
        address owner
    ) {
        // Retrieve the record from storage into memory for gas-efficient reading
        Record memory record = registry[_hash];
        
        // Return the tuple of values
        return (record.isRegistered, record.timestamp, record.owner);
    }
}