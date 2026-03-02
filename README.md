## Overview
This project implements a secure document verification system that anchors academic publications to the Ethereum blockchain. It guarantees content integrity and enables public verification without third-party reliance.

## Live Demo
Fully deployed on Microsoft Azure (Linux App Service).  
[Visit SRN Portal](https://srn-esg8b4gsfrhne4dj.germanywestcentral-01.azurewebsites.net)

## Key Features
- Blockchain Verification: Documents are hashed and stored on Ethereum (Sepolia/Mock) for tamper-proof verification.
- Role-Based Access: Distinct workflows for Guests, Researchers, and Administrators.
- Real-Time Updates: SignalR for instant blockchain transaction feedback.
- Secure Storage: Hybrid local/cloud storage with JWT authentication.

## System Architecture
Backend follows **Clean Architecture** (C# 12 + ASP.NET Core 8):

1. **Domain Layer** – Core entities (`Artifact`, `ApplicationUser`), business logic, repository interfaces (dependency-free).
2. **Application Layer** – Use cases, DTOs, service implementations.
3. **Infrastructure Layer** – EF Core (PostgreSQL), Nethereum smart-contract integration, SignalR hubs, file storage.
4. **API / Presentation Layer** – REST endpoints + vanilla HTML5/Bootstrap 5 frontend.

## Technology Stack
- **Backend**: ASP.NET Core Web API (.NET 8)
- **Frontend**: HTML5, Bootstrap 5, Vanilla JS, Fetch API
- **Database**: PostgreSQL + Entity Framework Core
- **Real-time**: SignalR
- **Blockchain**: Nethereum
- **Authentication**: ASP.NET Core Identity + JWT Bearer
- **Deployment**: Azure App Service (Linux) + GitHub Actions CI/CD

## Local Development Setup

### 1. Prerequisites
- .NET 8.0 SDK
- PostgreSQL

### 2. Clone Repository
```bash
git clone https://github.com/TianxingFan/SRN-Backend.git
cd SRN-Backend/SRN.API
```

### 3. Configuration
Create `appsettings.Development.json` in the `SRN.API` folder:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=SRN_Local;Username=postgres;Password=YOUR_PASSWORD;"
  },
  "JwtSettings": {
    "Key": "Your_Secret_Key_Must_Be_32_Characters_Long",
    "Issuer": "SRN",
    "Audience": "SRN",
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
