<div align="center">

# 📦 Order Service

**Enterprise-grade order management microservice for the xshopai e-commerce platform**

[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)](https://dotnet.microsoft.com)
[![C#](https://img.shields.io/badge/C%23-12-239120?style=for-the-badge&logo=csharp&logoColor=white)](https://learn.microsoft.com/dotnet/csharp/)
[![SQL Server](https://img.shields.io/badge/SQL_Server-2022-CC2927?style=for-the-badge&logo=microsoftsqlserver&logoColor=white)](https://www.microsoft.com/sql-server)
[![Dapr](https://img.shields.io/badge/Dapr-Enabled-0D597F?style=for-the-badge&logo=dapr&logoColor=white)](https://dapr.io)
[![License](https://img.shields.io/badge/License-MIT-green?style=for-the-badge)](LICENSE)

[Getting Started](#-getting-started) •
[Documentation](#-documentation) •
[API Reference](#-api-reference) •
[Contributing](#-contributing)

</div>

---

## 🎯 Overview

The **Order Service** handles order creation, status tracking, order history, and embeds an event consumer for status updates from the saga orchestrator. Built with a **single-process architecture** following industry best practices (Amazon, Netflix pattern), it provides REST APIs plus a background consumer — all in one deployment for optimal resource sharing and zero version skew.

---

## ✨ Key Features

<table>
<tr>
<td width="50%">

### 📦 Order Management

- Complete order CRUD operations
- Paginated order listing & search
- Customer order history tracking
- Order status lifecycle management

</td>
<td width="50%">

### 🔄 Embedded Event Consumer

- Single-process API + consumer architecture
- Subscribes to `order.status.changed` events
- Multi-broker support (RabbitMQ, Kafka, Azure Service Bus)
- Broker-agnostic via `IMessageBrokerAdapter`

</td>
</tr>
<tr>
<td width="50%">

### 📡 Event-Driven Architecture

- CloudEvents 1.0 specification
- Publishes OrderCreated, OrderCancelled events
- Consumes saga orchestrator status updates
- Cross-service synchronization

</td>
<td width="50%">

### 🛡️ Enterprise Security

- JWT Bearer token authentication
- Role-based access control (customer, admin)
- FluentValidation input validation
- SQL injection protection via EF Core

</td>
</tr>
</table>

---

## 🏗️ Architecture

**Single-Process Pattern (API + Embedded Consumer):**

```
REST API ─────────────┐
                      ├── Shared IOrderService ── SQL Server
Background Consumer ──┘
```

**Why Single Process?**

- ✅ No code duplication — shared business logic
- ✅ Single database connection pool
- ✅ One container, one process, one config
- ✅ API and consumer always in sync
- ✅ No inter-process communication overhead

---

## 🚀 Getting Started

### Prerequisites

- .NET 8 SDK
- SQL Server 2019+
- Docker & Docker Compose (optional)
- Dapr CLI (for production-like setup)

### Quick Start with Docker Compose

```bash
# Clone the repository
git clone https://github.com/xshopai/order-service.git
cd order-service

# Start SQL Server + service
docker-compose up -d

# Verify the service is healthy
curl http://localhost:8006/health
```

### Local Development Setup

<details>
<summary><b>🔧 Without Dapr (Simple Setup)</b></summary>

```bash
# Restore dependencies
dotnet restore

# Set up environment variables
cp .env.example .env
# Edit .env with your configuration

# Start SQL Server (Docker)
docker-compose -f docker-compose.db.yml up -d

# Apply migrations
dotnet ef database update

# Run the service
dotnet run --project OrderService.Api
```

📖 See [Local Development Guide](docs/LOCAL_DEVELOPMENT.md) for detailed instructions.

</details>

<details>
<summary><b>⚡ With Dapr (Production-like)</b></summary>

```bash
# Ensure Dapr is initialized
dapr init

# Start with Dapr sidecar
./run.sh       # Linux/Mac
.\run.ps1      # Windows

# Or manually
dapr run \
  --app-id order-service \
  --app-port 8006 \
  --dapr-http-port 3500 \
  --dapr-grpc-port 50001 \
  --resources-path .dapr/components \
  --config .dapr/config.yaml \
  -- dotnet run --project OrderService.Api
```

> **Note:** All services now use the standard Dapr ports (3500 for HTTP, 50001 for gRPC).

</details>

---

## 📚 Documentation

| Document                                          | Description                                        |
| :------------------------------------------------ | :------------------------------------------------- |
| 📘 [Local Development](docs/LOCAL_DEVELOPMENT.md) | Step-by-step local setup and development workflows |
| 📘 [Technical Reference](docs/TECHNICAL.md)       | Architecture, security, monitoring                 |
| ☁️ [Azure Container Apps](docs/ACA_DEPLOYMENT.md) | Deploy to serverless containers with built-in Dapr |
| 📝 [API Testing Guide](API_TESTING.md)            | Complete API testing examples with sample requests |

**API Documentation**: Swagger UI available at `/swagger` endpoint.

---

## 🔌 API Reference

| Method | Endpoint                            | Description            | Auth |
| :----- | :---------------------------------- | :--------------------- | :--- |
| `GET`  | `/`                                 | Health check           | No   |
| `GET`  | `/api/orders`                       | Get paginated orders   | Yes  |
| `POST` | `/api/orders`                       | Create new order       | Yes  |
| `GET`  | `/api/orders/{id}`                  | Get order by ID        | Yes  |
| `PUT`  | `/api/orders/{id}/status`           | Update order status    | Yes  |
| `GET`  | `/api/orders/customer/{customerId}` | Get orders by customer | Yes  |
| `GET`  | `/api/orders/search`                | Search with filters    | Yes  |

---

## 🧪 Testing

```bash
# Run all tests
dotnet test

# Build without tests
dotnet build

# Run with specific configuration
dotnet test --configuration Release

# Add migration
dotnet ef migrations add MigrationName

# Apply migration
dotnet ef database update
```

### Test Coverage

| Metric      | Status                   |
| :---------- | :----------------------- |
| Unit Tests  | ✅ xUnit                 |
| Integration | ✅ WebApplicationFactory |
| Validation  | ✅ FluentValidation      |

---

## 🏗️ Project Structure

```
order-service/
├── 📁 OrderService.Api/            # REST API + Embedded Consumer
│   ├── 📁 Controllers/             # REST endpoints
│   ├── 📁 Consumers/               # Background consumer service
│   └── 📄 Program.cs               # Application entry point
├── 📁 OrderService.Core/           # Shared business logic
│   ├── 📁 Services/                # Business logic layer
│   │   └── 📁 Messaging/           # Message broker adapters
│   ├── 📁 Repositories/            # Data access layer
│   ├── 📁 Models/
│   │   ├── 📁 Entities/            # Domain entities
│   │   ├── 📁 DTOs/                # Data transfer objects
│   │   ├── 📁 Events/              # Event contracts
│   │   └── 📁 Enums/               # Enumeration types
│   ├── 📁 Data/                    # EF Core context
│   ├── 📁 Configuration/           # Settings classes
│   ├── 📁 Validators/              # FluentValidation
│   └── 📁 Middlewares/             # Custom middlewares
├── 📁 OrderService.Tests/          # Unit tests
├── 📁 .dapr/                       # Dapr configuration
│   ├── 📁 components/              # Pub/sub, state stores
│   └── 📄 config.yaml              # Dapr runtime configuration
├── 📄 docker-compose.yml           # Full service stack
├── 📄 docker-compose.db.yml        # SQL Server only
├── 📄 Dockerfile                   # Production container image
└── 📄 OrderService.sln             # Solution file
```

---

## 🔧 Technology Stack

| Category          | Technology                                        |
| :---------------- | :------------------------------------------------ |
| 🟣 Runtime        | .NET 8 / C# 12                                    |
| 🌐 Framework      | ASP.NET Core 8 with Minimal API + Controllers     |
| 🗄️ Database       | SQL Server 2022 with Entity Framework Core        |
| ✅ Validation     | FluentValidation                                  |
| 📨 Messaging      | Dapr Pub/Sub (RabbitMQ, Kafka, Azure Service Bus) |
| 📋 Event Format   | CloudEvents 1.0 Specification                     |
| 🔐 Authentication | JWT Bearer Tokens                                 |
| 📖 API Docs       | Swagger / OpenAPI (Swashbuckle)                   |
| 🧪 Testing        | xUnit + WebApplicationFactory                     |
| 📊 Observability  | ILogger structured logging + correlation IDs      |

---

## ⚡ Quick Reference

```bash
# 🐳 Docker Compose
docker-compose up -d              # Start all services
docker-compose down               # Stop all services
docker-compose -f docker-compose.db.yml up -d  # SQL Server only

# 🟣 Local Development
dotnet run --project OrderService.Api  # Run service
dotnet watch --project OrderService.Api  # Hot reload

# ⚡ Dapr Development
./run.sh                          # Linux/Mac
.\run.ps1                         # Windows

# 🧪 Testing
dotnet test                       # Run all tests
dotnet build                      # Build solution

# 🔍 Health Check
curl http://localhost:8006/health
curl http://localhost:8006/swagger
```

---

## 📡 Events

### Published

| Event             | Description                    |
| :---------------- | :----------------------------- |
| `order.created`   | New order placed               |
| `order.cancelled` | Order cancelled by user/system |
| `order.updated`   | Order details changed          |

### Consumed

| Event                  | Source                         |
| :--------------------- | :----------------------------- |
| `order.status.changed` | Order Processor Service (saga) |

---

## 🤝 Contributing

We welcome contributions! Please follow these steps:

1. **Fork** the repository
2. **Create** a feature branch
   ```bash
   git checkout -b feature/amazing-feature
   ```
3. **Write** tests for your changes
4. **Run** the test suite
   ```bash
   dotnet test
   ```
5. **Commit** your changes
   ```bash
   git commit -m 'feat: add amazing feature'
   ```
6. **Push** to your branch
   ```bash
   git push origin feature/amazing-feature
   ```
7. **Open** a Pull Request

Please ensure your PR:

- ✅ Passes all existing tests
- ✅ Includes tests for new functionality
- ✅ Follows the existing code style
- ✅ Updates documentation as needed

---

## 🆘 Support

| Resource         | Link                                                                       |
| :--------------- | :------------------------------------------------------------------------- |
| 🐛 Bug Reports   | [GitHub Issues](https://github.com/xshopai/order-service/issues)           |
| 📖 Documentation | [docs/](docs/)                                                             |
| 📝 API Testing   | [API_TESTING.md](API_TESTING.md)                                           |
| 💬 Discussions   | [GitHub Discussions](https://github.com/xshopai/order-service/discussions) |

---

## 📄 License

This project is part of the **xshopai** e-commerce platform.
Licensed under the MIT License - see [LICENSE](LICENSE) for details.

---

<div align="center">

**[⬆ Back to Top](#-order-service)**

Made with ❤️ by the xshopai team

</div>
