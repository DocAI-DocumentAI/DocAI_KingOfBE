<div align="center">

# 🚀 DocAI - Enterprise Document Intelligence Platform

[![CI/CD Pipeline](https://github.com/DocAI-DocumentAI/DocAI_KingOfBE/actions/workflows/docker-publish.yml/badge.svg)](https://github.com/DocAI-DocumentAI/DocAI_KingOfBE/actions)
[![Docker Hub](https://img.shields.io/docker/pulls/magicflexing/docai-gateway?logo=docker)](https://hub.docker.com/u/magicflexing)
[![.NET](https://img.shields.io/badge/.NET-9.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

_AI-powered document management system built for enterprise scale_

[🎯 Features](#-features) • [🏗️ Architecture](#️-architecture) • [🚀 Quick Start](#-quick-start) • [📖 Documentation](#-documentation)

</div>

---

## 🎯 Features

### 🤖 AI-Powered Intelligence

- **Document Analysis**: Automated classification and content extraction
- **Smart Search**: Semantic search with vector embeddings
- **Chat Interface**: Natural language document queries via Ollama integration

### 🔐 Enterprise Security

- **JWT Authentication**: Stateless, scalable authentication
- **Role-Based Access Control**: Granular permissions (Admin, Manager, Editor, Member)
- **Department Isolation**: Multi-tenant document access control

### ⚡ Modern Architecture

- **Microservices**: 5 independent, scalable services
- **API Gateway**: Centralized routing with YARP
- **Event-Driven**: Real-time notifications and updates
- **Cloud Native**: Docker + Kubernetes ready

---

## 🏗️ Architecture

```mermaid
graph TB
    Client[Client Apps] --> Gateway[API Gateway :5000]

    Gateway --> Auth[Auth API :5001]
    Gateway --> Doc[Document API :5002]
    Gateway --> AI[AI API :5003]
    Gateway --> Notify[Notification API :5004]
    Gateway --> Chat[ChatBox API :5005]

    Auth --> AuthDB[(PostgreSQL)]
    Doc --> DocDB[(PostgreSQL)]
    AI --> Ollama[Ollama Server]
    Notify --> Redis[(Redis)]

    subgraph "AI Stack"
        AI --> |Text Generation| DeepSeek[DeepSeek-R1:1.5b]
        AI --> |Embeddings| Nomic[nomic-embed-text]
    end
```

### Service Breakdown

| Service              | Port | Responsibility               | Tech Stack         |
| -------------------- | ---- | ---------------------------- | ------------------ |
| **API Gateway**      | 5000 | Routing, Auth, Rate Limiting | YARP, .NET 9       |
| **Auth API**         | 5001 | JWT, Users, RBAC             | .NET 9, PostgreSQL |
| **Document API**     | 5002 | CRUD, Versioning, Search     | .NET 9, PostgreSQL |
| **AI API**           | 5003 | Document Analysis, ML        | .NET 9, Ollama     |
| **Notification API** | 5004 | Real-time Events             | .NET 9, Redis      |
| **ChatBox API**      | 5005 | Conversational AI            | .NET 9, Ollama     |

---

## 🚀 Quick Start

### Prerequisites

```bash
# Required
.NET 9 SDK, Docker, Git

# Optional (for local development)
PostgreSQL 15+, Redis 7+, Ollama
```

### 1️⃣ Clone & Setup

```bash
git clone https://github.com/DocAI-DocumentAI/DocAI_KingOfBE.git
cd DocAI_KingOfBE
```

### 2️⃣ Environment Configuration

```bash
# Copy environment template
cp .env.example .env

# Edit configuration
nano .env
```

<details>
<summary>📋 Environment Variables</summary>

```bash
# JWT Configuration
JWT__Secret=your-super-secret-key-here
JWT__Issuer=DocAI
JWT__Audience=DocAI-Users

# Database
ConnectionStrings__DefaultConnection=Host=localhost;Database=docai;Username=postgres;Password=yourpassword

# Redis (Optional)
ConnectionStrings__Redis=localhost:6379

# Ollama AI
Ollama__Host=http://localhost:11434
Ollama__TextGenerationModel=deepseek-r1:1.5b
Ollama__EmbeddingModel=nomic-embed-text:v1.5
```

</details>

### 3️⃣ Run with Docker (Recommended)

```bash
# Start all services
docker-compose up -d

# View logs
docker-compose logs -f

# Health check
curl http://localhost:5000/health
```

### 4️⃣ Access Services

| Service         | URL                           | Credentials |
| --------------- | ----------------------------- | ----------- |
| **API Gateway** | http://localhost:5000         | -           |
| **Swagger UI**  | http://localhost:5001/swagger | -           |
| **Dashboard**   | http://localhost:8080         | Admin panel |

---

## 📖 Documentation

### 🔐 Authentication Flow

```bash
# 1. Register user
POST /api/auth/register
{
  "email": "user@example.com",
  "password": "SecurePass123!",
  "role": "Member"
}

# 2. Login
POST /api/auth/login
{
  "email": "user@example.com",
  "password": "SecurePass123!"
}

# 3. Use token
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
```

### 🤖 AI Integration

```csharp
// Document analysis
POST /api/ai/analyze
{
  "documentId": "doc-123",
  "analysisType": "classification"
}

// Chat with documents
POST /api/chatbox/query
{
  "message": "Summarize the Q3 financial report",
  "documentContext": ["doc-123", "doc-456"]
}
```

### 🔒 Authorization System

```csharp
// Role-based access
[CustomAuthorize(Roles = new[] { Roles.Admin, Roles.Manager })]

// Department-based access
[CustomAuthorize(Departments = new[] { Departments.PhongNhanSu })]

// Permission-based access
[CustomAuthorize(Permissions = new[] { Permissions.ViewAnyDocument })]

// Combined authorization (AND logic)
[CustomAuthorize(
    Roles = new[] { Roles.Admin },
    Departments = new[] { Departments.Company },
    RequireAll = true
)]
```

---

## 🛠️ Development

### Local Development

```bash
# Run individual service
cd Services/Auth/Auth.API
dotnet run

# Run with hot reload
dotnet watch run

# Run tests
dotnet test
```

### Database Migrations

```bash
# Add migration
dotnet ef migrations add InitialCreate

# Update database
dotnet ef database update
```

### Docker Development

```bash
# Build specific service
docker build -f Services/Auth/Auth.API/Dockerfile -t docai-auth .

# Development with compose
docker-compose -f docker-compose.dev.yml up
```

---

## 🚀 Deployment

### Production Deployment

```bash
# Deploy to Kubernetes
kubectl apply -k k8s/

# Check deployment status
kubectl get pods -n docai
kubectl get ingress -n docai
```

### CI/CD Pipeline

- ✅ **Automated Testing**: Unit tests on every PR
- ✅ **Docker Build**: Multi-stage builds for optimization
- ✅ **Security Scanning**: Vulnerability checks
- ✅ **Auto Deploy**: Push to main → Deploy to production

---

## 📊 Performance & Monitoring

### Metrics

- **Response Time**: < 200ms (95th percentile)
- **Throughput**: 1000+ requests/second
- **Uptime**: 99.9% SLA
- **AI Processing**: < 5s for document analysis

### Monitoring Stack

- **Logging**: Serilog with structured logging
- **Metrics**: Prometheus + Grafana
- **Tracing**: OpenTelemetry
- **Health Checks**: Built-in endpoints

---

## 🤝 Contributing

We welcome contributions! Please see our [Contributing Guide](CONTRIBUTING.md).

### Development Workflow

1. Fork the repository
2. Create feature branch: `git checkout -b feature/amazing-feature`
3. Commit changes: `git commit -m 'Add amazing feature'`
4. Push to branch: `git push origin feature/amazing-feature`
5. Open Pull Request

### Code Standards

- ✅ Follow C# coding conventions
- ✅ Add unit tests for new features
- ✅ Update documentation
- ✅ Ensure CI/CD passes

---

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

---

## 🙏 Acknowledgments

- **Team**: King Of BE
- **AI Models**: DeepSeek-R1, Nomic Embeddings
- **Infrastructure**: .NET 9, Docker, Kubernetes

---

<div align="center">

**[⭐ Star this repo](https://github.com/DocAI-DocumentAI/DocAI_KingOfBE)** • **[🐛 Report Bug](https://github.com/DocAI-DocumentAI/DocAI_KingOfBE/issues)** • **[💡 Request Feature](https://github.com/DocAI-DocumentAI/DocAI_KingOfBE/issues)**

Made with ❤️ by [King Of BE Team](https://github.com/DocAI-DocumentAI)

</div>
