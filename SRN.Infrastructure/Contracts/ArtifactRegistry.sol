// SPDX-License-Identifier: MIT
pragma solidity ^0.8.20;

/**
 * @title IArtifactRegistry
 * @dev Core interface for the SRN Decentralized Verification System.
 * Defines the data structure and events for anchoring digital assets.
 */
interface IArtifactRegistry {
    
    struct Record {
        bytes32 documentHash; // The SHA-256 fingerprint of the file
        address owner;        // The wallet address that submitted the transaction
        uint256 timestamp;    // The block timestamp when the artifact was anchored
        bool isRegistered;    // Flag to prevent duplicate registrations
    }

    // Event emitted when a new artifact is successfully anchored.
    // Indexing parameters (indexed) allows for efficient searching in logs.
    event DocumentAnchored(
        bytes32 indexed docHash,
        address indexed owner,
        uint256 timestamp
    );

    // Registers a new artifact hash
    function registerArtifact(bytes32 _hash) external;

    // Verifies if a hash exists and returns its details
    function verifyArtifact(bytes32 _hash) external view returns (
        bool registered,
        uint256 timestamp,
        address owner
    );
}

/**
 * @title ArtifactRegistry
 * @dev Concrete implementation of the IArtifactRegistry interface.
 */
contract ArtifactRegistry is IArtifactRegistry {
    // State Variable: Maps a unique file hash to its registration record
    mapping(bytes32 => Record) private registry;

    /// @notice Anchors a file hash to the blockchain
    /// @param _hash The SHA-256 hash of the artifact
    function registerArtifact(bytes32 _hash) external override {
        // Step 1: Validation - Ensure the artifact hasn't been registered before
        require(!registry[_hash].isRegistered, "Artifact already registered");

        // Step 2: Storage - Create and save the record to the blockchain state
        registry[_hash] = Record({
            documentHash: _hash,
            owner: msg.sender, // In your current C# setup, this will be the Backend Service's address
            timestamp: block.timestamp,
            isRegistered: true
        });

        // Step 3: Event Emission - Emit an event so external systems (like your C# API) can confirm success
        emit DocumentAnchored(_hash, msg.sender, block.timestamp);
    }

    /// @notice Retrieves the registration details of a specific hash
    /// @param _hash The file hash to look up
    function verifyArtifact(bytes32 _hash) external view override returns (
        bool registered,
        uint256 timestamp,
        address owner
    ) {
        Record memory record = registry[_hash];
        return (record.isRegistered, record.timestamp, record.owner);
    }
}