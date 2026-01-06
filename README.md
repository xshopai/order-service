# 📭 Order Service

Order management microservice for xshopai - handles order creation, status tracking, order history, and embedded event consumer for status updates from saga orchestrator.

## 🚀 Quick Start

### Prerequisites

- **.NET 8 SDK** ([Download](https://dotnet.microsoft.com/download/dotnet/8.0))
- **PostgreSQL** 12+ ([Download](https://www.postgresql.org/download/))
- **Dapr CLI** 1.16+ ([Install Guide](https://docs.dapr.io/getting-started/install-dapr-cli/))

### Setup

**1. Start PostgreSQL**
```bash
# Using Docker (recommended)
docker run -d --name order-postgres -p 5432:5432 \
  -e POSTGRES_PASSWORD=postgres \
  -e POSTGRES_DB=orderservice_dev \
  postgres:12

# Or install PostgreSQL locally
```

**2. Clone & Restore**
```bash
git clone https://github.com/xshopai/order-service.git
cd order-service
dotnet restore
```

**3. Configure Environment**
```bash
# Copy environment template
cp .env.example .env

# Edit .env - update these values:
# ConnectionStrings__DefaultConnection=Host=localhost;Database=orderservice_dev;Username=postgres;Password=postgres
# Jwt__Key=your-secret-key-min-32-characters
```

**4. Apply Migrations**
```bash
# Create database
createdb orderservice_dev

# Apply migrations
dotnet ef database update
```

**5. Run Service**
```bash
# Start with Dapr (recommended)
./run.sh       # Linux/Mac
.\run.ps1      # Windows

# Or run directly
dotnet run
```

**6. Verify**
```bash
# Check health
curl http://localhost:7000/health

# Swagger UI
Open https://localhost:5001/swagger
```

### Common Commands

```bash
# Run tests
dotnet test

# Build
dotnet build

# Add migration
dotnet ef migrations add MigrationName

# Remove migration
dotnet ef migrations remove

# Production mode
dotnet run --configuration Release
```

## 📚 Documentation

| Document | Description |
|----------|-------------|
| [📖 Developer Guide](docs/DEVELOPER_GUIDE.md) | Local setup, debugging, daily workflows |
| [📘 Technical Reference](docs/TECHNICAL.md) | Architecture, security, monitoring |
| [🤝 Contributing](docs/CONTRIBUTING.md) | Contribution guidelines and workflow |
| [📝 API Testing Guide](API_TESTING.md) | Complete API testing examples |

**API Documentation**: Swagger UI available at `/swagger` endpoint.

## ⚙️ Configuration

### Embedded Consumer Architecture

The Order Service uses a **single-process architecture** with an embedded event consumer, following industry best practices (Amazon, Netflix pattern):

**OrderService.Api** (REST API + Consumer)

- **Publishes events** via HTTP to `message-broker-service` (for OrderCreated, OrderCancelled, etc.)
- **Consumes events** directly from message broker (RabbitMQ, Kafka, or Azure Service Bus)
- Uses environment variables: `MESSAGE_BROKER_SERVICE_URL` (default: http://localhost:4000)
- Configuration: `MessageBroker` section in appsettings.json

**Why Single Process?**

- ✅ **No code duplication** - Shared business logic in single deployment
- ✅ **Single database connection pool** - Better resource utilization
- ✅ **Simplified deployment** - One container, one process, one configuration
- ✅ **No version skew** - API and consumer always in sync
- ✅ **Easier monitoring** - Single process to monitor and debug
- ✅ **Better performance** - No inter-process communication overhead

**Embedded Consumer:**

The API includes `OrderStatusConsumerService` as a BackgroundService that:

- Subscribes to `order.status.changed` events from Order Processor Service
- Updates order status in database using the same `IOrderService` as the API
- Supports **any message broker** (RabbitMQ, Kafka, Azure Service Bus) via `IMessageBrokerAdapter`
- Runs in the same process as the REST API for optimal resource sharing

### Project Structure

```
OrderService/
├── OrderService.Api/        # REST API + Embedded Consumer
│   ├── Controllers/        # REST endpoints
│   └── Consumers/          # Background consumer service
├── OrderService.Core/       # Shared business logic
│   ├── Services/           # Business logic layer
│   │   ├── Messaging/      # Message broker adapters
│   │   └── IOrderService   # Service interfaces
│   ├── Repositories/       # Data access layer
│   ├── Models/
│   │   ├── Entities/       # Domain entities
│   │   ├── DTOs/          # Data transfer objects
│   │   ├── Events/        # Event contracts
│   │   └── Enums/         # Enumeration types
│   ├── Data/              # EF Core context and configurations
│   ├── Configuration/     # Application settings classes
│   ├── Validators/        # FluentValidation validators
│   ├── Middlewares/       # Custom middlewares
│   └── Extensions/        # Extension methods
└── OrderService.Tests/    # Unit tests
```

### Technology Stack

- **Framework**: ASP.NET Core 8
- **Database**: PostgreSQL with Entity Framework Core
- **Authentication**: JWT Bearer tokens
- **Validation**: FluentValidation
- **Messaging**: RabbitMQ & Azure Service Bus
- **Documentation**: Swagger/OpenAPI
- **Serialization**: System.Text.Json

## 🔧 Configuration

### Environment Setup

1. Copy `.env.example` to `.env`
2. Update the values in `.env` with your configuration

### Database Connection

```bash
ConnectionStrings__DefaultConnection=Host=localhost;Database=orderservice_dev;Username=username;Password=password
```

### JWT Settings

```bash
Jwt__Key=your-secret-key-min-32-characters
Jwt__Issuer=OrderService
Jwt__Audience=OrderService.Users
Jwt__ExpiryInMinutes=60
```

### Message Broker Configuration

The Order Service supports **multiple message brokers** for maximum flexibility:

```bash
# RabbitMQ (default)
MessageBroker__Provider=RabbitMQ
MessageBroker__RabbitMQ__ConnectionString=amqp://guest:guest@localhost:5672/
MessageBroker__RabbitMQ__Exchange=xshopai.events
MessageBroker__RabbitMQ__ExchangeType=topic

# Kafka (alternative)
MessageBroker__Provider=Kafka
MessageBroker__Kafka__BootstrapServers=localhost:9092
MessageBroker__Kafka__GroupId=order-service-group

# Azure Service Bus (alternative)
MessageBroker__Provider=AzureServiceBus
MessageBroker__AzureServiceBus__ConnectionString=Endpoint=sb://...
MessageBroker__AzureServiceBus__TopicName=order-events
```

The embedded consumer automatically uses the configured broker via the `IMessageBrokerAdapter` interface.

## 🚀 Getting Started

### Prerequisites

- .NET 8 SDK
- PostgreSQL 12+
- RabbitMQ (optional, for event publishing)
- Azure Service Bus (optional, alternative to RabbitMQ)

### Installation

1. **Clone the repository**

   ```bash
   git clone [repository-url]
   cd order-service
   ```

2. **Install dependencies**

   ```bash
   dotnet restore
   ```

3. **Configure environment**

   ```bash
   cp .env.example .env
   # Edit .env with your settings
   ```

4. **Setup database**

   ```bash
   # Create database
   createdb orderservice_dev

   # Apply migrations
   dotnet ef database update
   ```

5. **Run the application**

   ```bash
   dotnet run
   ```

6. **Access Swagger UI**
   - Navigate to `https://localhost:5001/swagger`

## 📋 API Endpoints

### Core Endpoints

| Method | Endpoint                            | Description                   | Auth Required |
| ------ | ----------------------------------- | ----------------------------- | ------------- |
| GET    | `/`                                 | Health check and service info | No            |
| GET    | `/api/orders`                       | Get paginated orders          | Yes           |
| POST   | `/api/orders`                       | Create new order              | Yes           |
| GET    | `/api/orders/{id}`                  | Get order by ID               | Yes           |
| PUT    | `/api/orders/{id}/status`           | Update order status           | Yes           |
| GET    | `/api/orders/customer/{customerId}` | Get orders by customer        | Yes           |
| GET    | `/api/orders/search`                | Search orders with filters    | Yes           |

### Authentication

- All endpoints (except health check) require JWT authentication
- Include `Authorization: Bearer {token}` header
- Roles supported: `customer`, `admin`

## 🔄 Event-Driven Architecture

The service publishes and consumes events via a configurable message broker:

### Events Published

- **OrderCreatedEvent**: Published when a new order is created
- **OrderCancelledEvent**: Published when an order is cancelled
- **OrderUpdatedEvent**: Published when order details change

### Events Consumed

- **OrderStatusChangedEvent**: Consumed from Order Processor Service (saga orchestrator)
  - Updates order status based on saga execution results
  - Reflects Payment, Inventory, and Shipping service outcomes

### Message Broker Support

- **RabbitMQ**: Topic-based routing with configurable exchanges
- **Kafka**: Consumer groups with topic subscriptions
- **Azure Service Bus**: Topic/subscription pattern with managed identity support
- **Broker-Agnostic**: Switch between providers via configuration without code changes

## 🧪 Testing

### API Testing

See [API_TESTING.md](API_TESTING.md) for comprehensive API testing examples.

### Sample Test Data

```json
{
  "customerId": "507f1f77bcf86cd799439011",
  "productId": "507f1f77bcf86cd799439012"
}
```

## 🛠️ Development

### Using VS Code Debug

- Use the "Debug Order Service" configuration for standard debugging
- Use "Debug Order Service (Hot Reload)" for development with automatic reloading

### Database Migrations

```bash
# Add migration
dotnet ef migrations add MigrationName

# Apply migration
dotnet ef database update

# Remove last migration
dotnet ef migrations remove
```

## 🔒 Security

- JWT token authentication
- Role-based authorization
- Input validation with FluentValidation
- SQL injection protection via EF Core
- HTTPS enforcement in production
- Structured error responses (no sensitive data exposure)

## 📊 Monitoring & Logging

- Comprehensive logging using `ILogger`
- Structured logging with correlation IDs
- Error tracking and debugging support
- Performance monitoring capabilities
- Health check endpoints

## 🚀 Deployment

### Environment Variables

- `ASPNETCORE_ENVIRONMENT`: Environment name
- `ConnectionStrings__DefaultConnection`: Database connection
- `Jwt__Key`: JWT signing key
- `MessageBroker__Provider`: Message broker provider

## 🤝 Contributing

1. Fork the repository
2. Create a feature branch
3. Commit your changes
4. Push to the branch
5. Create a Pull Request

## 📄 License

This project is licensed under the MIT License - see the LICENSE file for details.

## 🆘 Support

For questions and support:

- Check the [API Testing Guide](API_TESTING.md)
- Review the Swagger documentation
- Check application logs for debugging
- Create an issue for bug reports or feature requests

---

**Built with ❤️ using ASP.NET Core 8**
