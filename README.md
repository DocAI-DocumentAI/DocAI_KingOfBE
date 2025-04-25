# DocAI - Document Management System Backend

## Overview

DocAI is a modern, microservices-based document management system backend built with .NET. The system provides comprehensive document handling capabilities with AI integration, authentication, and real-time notifications.

## Architecture

The system is built using a microservices architecture with the following components:

### Services

1. **API Gateway (Port: 5000)**

   - Central entry point for all client requests
   - Handles request routing and load balancing
   - Implements cross-cutting concerns

2. **Auth API (Port: 5001)**

   - Handles user authentication and authorization
   - Manages user sessions and security tokens
   - User management functionality

3. **Document API (Port: 5002)**

   - Core document management functionality
   - Document CRUD operations
   - Document versioning and metadata management

4. **AI API (Port: 5003)**

   - AI-powered document processing
   - Document analysis and insights
   - Intelligent document classification

5. **Notification API (Port: 5004)**
   - Real-time notification system
   - Event-driven updates
   - User notification preferences

## Technologies Used

- **.NET Core** - Main development framework
- **Docker** - Containerization
- **Docker Compose** - Container orchestration
- **Microservices Architecture** - System design pattern
- **RESTful APIs** - Service communication

## Prerequisites

- .NET 6.0 or later
- Docker Desktop
- Git

## Getting Started

### Installation

1. Clone the repository:

```bash
git clone https://github.com/yourusername/DocAI_KingOfBE.git
cd DocAI_KingOfBE
```

2. Build and run the services using Docker Compose:

```bash
docker-compose up --build
```

### Service Endpoints

After starting the services, they will be available at:

- API Gateway: `http://localhost:5000`
- Auth API: `http://localhost:5001`
- Document API: `http://localhost:5002`
- AI API: `http://localhost:5003`
- Notification API: `http://localhost:5004`

## Project Structure

```
DocAI_KingOfBE/
├── ApiGateway/           # API Gateway service
├── Auth.API/             # Authentication service
├── Document.API/         # Document management service
├── AI.API/               # AI processing service
├── Notification.API/     # Notification service
├── Shared/              # Shared libraries and utilities
├── compose.yaml         # Docker compose configuration
└── README.md           # Project documentation
```

## Contributing

1. Fork the repository
2. Create your feature branch (`git checkout -b feature/AmazingFeature`)
3. Commit your changes (`git commit -m 'Add some AmazingFeature'`)
4. Push to the branch (`git push origin feature/AmazingFeature`)
5. Open a Pull Request

## License

This project is licensed under the MIT License - see the LICENSE file for details.

## Contact

Your Name - nguyenhuyphc@gmail.com
Project Link: https://github.com/DocAI-DocumentAI/DocAI_KingOfBE
