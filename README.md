# DocAI - Document Management System Backend

## 🚀 Vision

DocAI aims to be a robust, scalable, and intelligent backend platform for document management, integrating AI-powered document processing, secure authentication, and real-time notifications. Designed for enterprise and research use, DocAI is built with modern microservices architecture and is ready for cloud-native deployment.

---

## ✨ Features

- **Microservices Architecture**: Decoupled, independently deployable services
- **AI Document Processing**: Automated document analysis and classification
- **Secure Authentication**: JWT-based authentication and role-based authorization
- **Real-time Notifications**: Event-driven user notifications
- **API Gateway**: Centralized routing, security, and monitoring
- **Cloud Native**: Ready for Docker and Kubernetes
- **OpenAPI/Swagger**: Interactive API documentation
- **Extensible**: Easily add new services or integrations

---

## 🏗️ Architecture

```
+-------------------+      +-------------------+      +-------------------+
|                   |      |                   |      |                   |
|   API Gateway     +----->+   Auth API        |      |   AI API          |
|  (Load Balancer)  |      | (JWT, Users)      |      | (AI/ML)           |
|                   |      |                   |      |                   |
+-------------------+      +-------------------+      +-------------------+
        |                        |                        |
        v                        v                        v
+-------------------+      +-------------------+      +-------------------+
|                   |      |                   |      |                   |
| Document API      |      | Notification API  |      | Shared Services   |
| (CRUD, Version)   |      | (Events, Alerts)  |      | (DB, Redis, etc.) |
+-------------------+      +-------------------+      +-------------------+
```

---

## 🛠️ Tech Stack

- **.NET 9** (C#)
- **Docker & Docker Compose**
- **Kubernetes** (YAML manifests)
- **NSwag** (OpenAPI/Swagger UI)
- **Serilog** (Logging)
- **PostgreSQL** (default DB, can be swapped)
- **Redis** (caching, optional)
- **CI/CD**: GitHub Actions (sample workflow provided)

---

## ⚙️ Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/9.0)
- [Docker](https://www.docker.com/products/docker-desktop)
- [kubectl](https://kubernetes.io/docs/tasks/tools/) (for Kubernetes)
- [Git](https://git-scm.com/)
- (Optional) [PostgreSQL](https://www.postgresql.org/) and [Redis](https://redis.io/)

---

## 🔑 Environment Variables

Each service can be configured via environment variables or `appsettings.json`. Key variables:

- `JWT__Secret`: Secret key for JWT signing
- `JWT__Issuer`: JWT issuer string
- `ConnectionStrings__DefaultConnection`: PostgreSQL connection string
- `ConnectionStrings__Redis`: Redis connection string (if used)

For Docker/K8s, use `.env` files or Kubernetes secrets/configmaps.

---

## 🧑‍💻 Local Development

1. **Clone the repository:**
   ```bash
   git clone https://github.com/DocAI-DocumentAI/DocAI_KingOfBE.git
   cd DocAI_KingOfBE
   ```
2. **Configure environment:**
   - Copy and edit `appsettings.Development.json` or set environment variables as needed.
3. **Run a service:**
   ```bash
   cd Auth.API
   dotnet run
   ```
4. **Access Swagger UI:**
   - Example: [http://localhost:5001/swagger](http://localhost:5001/swagger)

---

## 🐳 Running with Docker Compose

1. **Build and start all services:**
   ```bash
   docker-compose up --build
   ```
2. **Access services:**
   - API Gateway: [http://localhost:5000](http://localhost:5000)
   - Auth API: [http://localhost:5001](http://localhost:5001)
   - Document API: [http://localhost:5002](http://localhost:5002)
   - AI API: [http://localhost:5003](http://localhost:5003)
   - Notification API: [http://localhost:5004](http://localhost:5004)

---

## ☸️ Running on Kubernetes

1. **Apply all manifests:**
   ```bash
   kubectl create namespace docai
   kubectl apply -k k8s/
   ```
2. **Check status:**
   ```bash
   kubectl get all -n docai
   kubectl get ingress -n docai
   ```
3. **(Optional) Expose via Ingress:**
   - See `k8s/ingress.yaml` for path-based routing.
4. **Set secrets/configmaps:**
   - Use `kubectl create secret` or `kubectl create configmap` for sensitive data.

---

## 🔄 CI/CD (GitHub Actions)

- Sample workflow provided in `.github/workflows/ci-cd.yml`:
  - Build and push Docker images to Docker Hub
  - Deploy to VPS or Kubernetes cluster
- **Secrets required:**
  - `DOCKER_HUB_USERNAME`, `DOCKER_HUB_TOKEN`
  - `VPS_HOST`, `VPS_USERNAME`, `VPS_SSH_KEY` (if deploying to VPS)

---

## 📚 API Documentation

- **Swagger UI** is available for each service in development mode.
- Auth API: [http://localhost:5001/swagger](http://localhost:5001/swagger)
- See `/swagger` endpoint of each service for full API docs.

---

## 🛡️ Troubleshooting Authentication (JWT)

- Use the **Authorize** button in Swagger UI and paste your token with the `Bearer` prefix:
  ```
  Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
  ```
- If you get `User is not authenticated` or `User ID claim not found in token`:
  - Check that the `Authorization` header is present in your request (see browser dev tools > Network > Headers).
  - Make sure your token is not expired and matches the API's JWT secret and issuer.
  - If using Kubernetes, ensure your environment variables/secrets are set correctly for each deployment.
  - Ensure your endpoint is decorated with `[Authorize]` attribute.

---

## 🤝 Contributing

1. Fork the repository
2. Create your feature branch (`git checkout -b feature/your-feature`)
3. Commit your changes (`git commit -m 'Add your feature'`)
4. Push to the branch (`git push origin feature/your-feature`)
5. Open a Pull Request
6. Please follow the code style and add tests where appropriate

---

## 📄 License

This project is licensed under the MIT License. See the LICENSE file for details.

---

## 📬 Contact

- **Team:** King Of BE
- **Email:** nguyenhuyphc@gmail.com
- **Project:** [https://github.com/DocAI-DocumentAI/DocAI_KingOfBE](https://github.com/DocAI-DocumentAI/DocAI_KingOfBE)
