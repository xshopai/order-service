# Order Service - Local Development Guide

## Prerequisites

- .NET 8 SDK
- PostgreSQL 14+ (local or Docker)
- Dapr CLI (for pub/sub and service invocation)

## Quick Start

### 1. Start PostgreSQL

```bash
docker run -d \
  --name postgres-order \
  -e POSTGRES_USER=orderadmin \
  -e POSTGRES_PASSWORD=orderpass \
  -e POSTGRES_DB=order_db \
  -p 5434:5432 \
  postgres:14
```

### 2. Configure Application

Update `OrderService.Api/appsettings.Development.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5434;Database=order_db;Username=orderadmin;Password=orderpass"
  },
  "Dapr": {
    "HttpPort": 3500,
    "PubSubName": "xshopai-pubsub"
  }
}
```

> **Note:** All services now use the standard Dapr ports (3500 for HTTP, 50001 for gRPC). This simplifies configuration and works consistently whether running via Docker Compose or individual service runs.

### 3. Run Database Migrations

```bash
cd OrderService.Api
dotnet ef database update
```

### 4. Run the Service

Without Dapr:

```bash
dotnet run --project OrderService.Api
```

With Dapr:

```bash
./run.sh
# or on Windows
./run.ps1
```

## Project Structure

```
order-service/
├── OrderService.Api/           # Web API project
│   ├── Controllers/            # API controllers
│   ├── Services/               # Business logic
│   ├── Models/                 # Domain models
│   ├── Data/                   # EF Core context
│   └── Program.cs              # Application entry
├── OrderService.Tests/         # Unit tests
├── OrderService.sln            # Solution file
└── Dockerfile
```

## API Endpoints

| Method | Endpoint                    | Description         |
| ------ | --------------------------- | ------------------- |
| GET    | `/health`                   | Health check        |
| POST   | `/api/orders`               | Create new order    |
| GET    | `/api/orders/{id}`          | Get order by ID     |
| GET    | `/api/orders/user/{userId}` | Get user's orders   |
| PUT    | `/api/orders/{id}/status`   | Update order status |
| DELETE | `/api/orders/{id}`          | Cancel order        |

## Testing

```bash
# Run all tests
dotnet test

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"
```

## Published Events

| Event             | Trigger              |
| ----------------- | -------------------- |
| `order.created`   | New order placed     |
| `order.updated`   | Order status changed |
| `order.cancelled` | Order cancelled      |
| `order.completed` | Order fulfilled      |

## Troubleshooting

### EF Migrations Fail

```bash
dotnet ef migrations add InitialCreate --project OrderService.Api
```

### Database Connection Issues

- Verify PostgreSQL is running on port 5434
- Check connection string format for Npgsql
