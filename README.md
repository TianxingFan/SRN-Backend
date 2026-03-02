# 🎓 Strategic Research Nexus (SRN)

![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet&logoColor=white)
![PostgreSQL](https://img.shields.io/badge/PostgreSQL-16-336791?logo=postgresql&logoColor=white)
[![Live Demo](https://img.shields.io/badge/Live_Demo-Azure-0078D4?logo=microsoftazure&logoColor=white)](https://srn-nexus.azurewebsites.net)
![License](https://img.shields.io/badge/license-MIT-green)

**Strategic Research Nexus (SRN)** is an independent academic platform connecting Afghan researchers worldwide. We bridge academic divides to foster interdisciplinary collaboration and advance evidence-based research for Afghanistan's sustainable development.

This repository contains the source code for the SRN Web Portal and its decentralized document verification system.

## 🚀 Live Demo

The platform is fully deployed and live on Microsoft Azure (Linux App Service).

👉 **[Click here to visit the SRN Portal](https://srn-nexus.azurewebsites.net)**

---

## ✨ Key Features

- **Zero-Trust Document Verification**: All approved publications are cryptographically anchored to the Ethereum blockchain. Users can independently verify the provenance and integrity of any downloaded PDF using its SHA-256 hash.
- **Role-Based Workflows**: 
  - **Guests**: Browse, download, and verify public academic papers.
  - **Researchers**: Dedicated workspace to submit manuscripts for peer review.
  - **Administrators**: Editorial dashboard to review, approve, and seamlessly publish manuscripts to the ledger.
- **Real-Time Notifications**: Integrated with SignalR (WebSockets) to provide instant feedback during long-running tasks like blockchain transaction processing.
- **Robust Security**: JWT-based authentication, role-based access control (RBAC), and secure local/cloud file storage.

---

## 🏗️ System Architecture

The backend is built with **C# 12 and ASP.NET Core 8**, strictly following the **Clean Architecture** principles to ensure separation of concerns, testability, and maintainability:

1. **Domain Layer**: Contains enterprise logic, core entities (`Artifact`, `ApplicationUser`), and repository interfaces. Completely dependency-free.
2. **Application Layer**: Contains business use cases, DTOs, and Service implementations.
3. **Infrastructure Layer**: Handles external concerns—Entity Framework Core (PostgreSQL), Nethereum Smart Contract interactions, SignalR Hubs, and File Storage.
4. **API / Presentation Layer**: RESTful endpoints and the vanilla HTML5/Bootstrap5 frontend interface.

---

## 🛠️ Technology Stack

* **Backend**: ASP.NET Core Web API (.NET 8)
* **Frontend**: HTML5, Bootstrap 5, Vanilla JavaScript, Fetch API
* **Database**: PostgreSQL (Entity Framework Core)
* **Real-time Engine**: SignalR
* **Blockchain Integration**: Nethereum (Ethereum Web3 for .NET)
* **Authentication**: ASP.NET Core Identity + JWT Bearer Tokens
* **Deployment**: Azure App Service (Linux) + GitHub Actions CI/CD

---

## 💻 Getting Started (Local Development)

Follow these steps to run the backend locally.

### Prerequisites
* [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
* A PostgreSQL instance (Local or Docker)

### 1. Clone the repository
```bash
git clone [https://github.com/TianxingFan/SRN-Backend.git](https://github.com/TianxingFan/SRN-Backend.git)
cd SRN-Backend/SRN.API
