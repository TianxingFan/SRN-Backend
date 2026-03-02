# 🎓 Strategic Research Nexus (SRN)

**Strategic Research Nexus (SRN)** is an independent academic platform connecting Afghan researchers worldwide. We bridge academic divides to foster interdisciplinary collaboration and advance evidence-based research for Afghanistan's sustainable development.

This repository contains the source code for the SRN Web Portal and its decentralized document verification system.

## Live Demo

The platform is fully deployed and live on Microsoft Azure (Linux App Service).

**[Click here to visit the SRN Portal](srn-esg8b4gsfrhne4dj.germanywestcentral-01.azurewebsites.net)**

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
* **Deployment**: Azure App Service (Linux) + GitHub Actions CI/CD

---

## Getting Started (Local Development)

Follow these steps to run the backend locally.

### Prerequisites
* [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
* A PostgreSQL instance (Local or Docker)

### 1. Clone the repository
```bash
git clone https://github.com/TianxingFan/SRN-Backend.git
cd SRN-Backend/SRN.API
```

### 2. Configure Local Environment (Crucial)

Since sensitive configuration files are git-ignored for security, you must create a local configuration file manually.

1. Create a new file named `appsettings.Development.json` inside the SRN.API folder.
2. Paste the following JSON content into it.
3. Update the `DefaultConnection` with your local PostgreSQL credentials.

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=SRN_Local_Db;Username=postgres;Password=YOUR_LOCAL_DB_PASSWORD;"
  },
  "JwtSettings": {
    "Key": "This_Is_A_Local_Dev_Secret_Key_For_Testing_Only_123456",
    "Issuer": "SRN.API",
    "Audience": "SRN.Client",
    "DurationInMinutes": 60
  },
  "Blockchain": {
    "Provider": "Mock", 
    "RpcUrl": "",
    "PrivateKey": "",
    "ContractAddress": ""
  }
}
```

### 3. Run the Application

The application is configured to automatically apply database migrations on startup.

```bash
dotnet run
```

Wait for the console to display Now listening on: http://localhost:xxxx.

---

### 4. Access the Portal

Open your browser and navigate to the localhost URL shown in your terminal:

```
http://localhost:5213/index.html
```

Default Admin Credentials:
Email: admin@srn.ie
Password: Admin@123456
