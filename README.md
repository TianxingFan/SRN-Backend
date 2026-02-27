# Strategic Research Nexus (SRN)

**SRN** is An independent academic platform connecting Afghan researchers worldwide. We bridge academic divides to foster interdisciplinary collaboration and advance evidence-based research for Afghanistan's sustainable development.

This repository contains the source code for the SRN Web Portal and its decentralized document verification system.

---

## Key Features

- **Zero-Trust Document Verification**: All approved publications are cryptographically anchored to the Ethereum blockchain. Users can independently verify the provenance and integrity of any downloaded PDF using its SHA-256 hash.
- **Role-Based Workflows**: 
  - **Guests**: Browse, download, and verify public academic papers.
  - **Researchers**: Dedicated workspace to submit manuscripts for peer review.
  - **Administrators**: Editorial dashboard to review, approve, and seamlessly publish manuscripts to the ledger.
- **Real-Time Notifications**: Integrated with SignalR (WebSockets) to provide instant feedback during long-running tasks like blockchain transaction processing.
- **Robust Security**: JWT-based authentication, role-based access control (RBAC), and secure local/cloud file storage.

---

## System Architecture

The backend is built with **C# 12 and ASP.NET Core 8**, strictly following the **Clean Architecture** principles to ensure separation of concerns, testability, and maintainability:

1. **Domain Layer**: Contains enterprise logic, core entities (`Artifact`, `ApplicationUser`), and repository interfaces. Completely dependency-free.
2. **Application Layer**: Contains business use cases, DTOs, and Service implementations.
3. **Infrastructure Layer**: Handles external concerns—Entity Framework Core (PostgreSQL), Nethereum Smart Contract interactions, SignalR Hubs, and File Storage.
4. **API / Presentation Layer**: RESTful endpoints and the vanilla HTML5/Bootstrap5 frontend interface.

---

## Technology Stack

* **Backend**: ASP.NET Core Web API (.NET 8)
* **Frontend**: HTML5, Bootstrap 5, Vanilla JavaScript, Fetch API
* **Database**: PostgreSQL (Entity Framework Core)
* **Real-time Engine**: SignalR
* **Blockchain Integration**: Nethereum (Ethereum Web3 for .NET)
* **Authentication**: ASP.NET Core Identity + JWT Bearer Tokens

---

## Getting Started (Local Development)

### Prerequisites
* [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
* A PostgreSQL instance (Local or Cloud like [Neon.tech](https://neon.tech/))

### 1. Clone the repository
```bash
git clone [https://github.com/TianxingFan/SRN-Backend.git]
cd SRN_Backend/SRN.API

### 2. Configure Environment Variables

Create an `appsettings.Development.json` file inside the `SRN.API` directory  
(or alternatively use **Visual Studio User Secrets** for better security in development).

Add the following configuration:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=your_postgres_host;Database=srndb;Username=postgres;Password=yourpassword;"
  },
  "JwtSettings": {
    "Key": "Your_Super_Secret_Key_Must_Be_Longer_Than_32_Chars!",
    "Issuer": "SRNTeam",
    "Audience": "SRNUsers",
    "DurationInMinutes": 120
  },
  "Blockchain": {
    "Provider": "Mock", 
    // Use "Real" for testnet or mainnet deployment
    "PrivateKey": "YOUR_ETH_WALLET_PRIVATE_KEY"
  }
}
```

> ⚠️ **Important Security Notes**
>
> - Never commit real database credentials or private keys to GitHub.
> - Use environment variables or `dotnet user-secrets` in production.
> - Ensure your JWT key is at least 32 characters long.

---

### 3. Apply Database Migrations

The application is configured to automatically apply **EF Core migrations** on startup.

Simply run:

```bash
dotnet run
```

On first launch, the system will:

- Create all necessary database tables
- Seed the default **Admin** account
- Initialize required roles

---

### 4. Access the Portal

Once the application is running, open your browser and navigate to:

```
https://localhost:xxxx/index.html
```

(Replace `xxxx` with the port shown in your console.)

---

### Default Admin Credentials (Auto-Seeded)

```
Email:    admin@srn.ie
Password: Admin@123456
```

> ⚠️ It is strongly recommended to change the default password immediately after first login.
