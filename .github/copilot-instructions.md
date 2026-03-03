# Copilot Instructions — order-service

## Service Identity

- **Name**: order-service
- **Purpose**: Order management — CRUD, lifecycle tracking, returns, cancellations, order events
- **Port**: 8006
- **Language**: C# 12 / .NET 8
- **Framework**: ASP.NET Core 8 Web API
- **Database**: SQL Server (port 1434) via Entity Framework Core 8
- **Dapr App ID**: `order-service`

## Architecture

- **Pattern**: Clean layered — Controllers → Services → Repositories → EF Core DbContext
- **API Style**: RESTful with Swagger/OpenAPI via Swashbuckle
- **Authentication**: JWT Bearer tokens via ASP.NET Core Authentication
- **Messaging**: Dapr pub/sub (publisher + embedded BackgroundService consumer)
- **Event Format**: CloudEvents 1.0 specification
- **Solution**: Multi-project — `OrderService.Api` (host) + `OrderService.Core` (domain/data) + `OrderService.Tests`

## Project Structure

```
order-service/
├── OrderService.Api/
│   ├── Program.cs               # Application bootstrap
│   ├── Controllers/             # API endpoints
│   └── Migrations/              # EF Core migrations
├── OrderService.Core/
│   ├── Data/                    # DbContext, entity configs
│   ├── Models/                  # Domain entities + DTOs
│   ├── Repositories/            # Data access interfaces + implementations
│   ├── Services/                # Business logic
│   ├── Messaging/               # IMessagingProvider (Dapr + RabbitMQ)
│   ├── Events/                  # Event models + publishers + consumers
│   ├── Validators/              # FluentValidation validators
│   ├── Extensions/              # DI registration extensions
│   └── Utils/                   # StandardLogger, helpers
├── OrderService.Tests/          # xUnit test project
├── .dapr/components/
└── OrderService.sln
```

## Code Conventions

- **C# 12** with nullable reference types enabled
- Use **Entity Framework Core 8** with code-first migrations
- Use **FluentValidation** for request validation
- Use **Serilog** for structured logging (console + file)
- Use **Dapr.AspNetCore** + **Dapr.Client** for pub/sub and service invocation
- Dependency injection via extension methods in `OrderService.Core/Extensions/`
- `IOrderService` / `IOrderRepository` interface patterns
- `ICurrentUserService` extracts user from JWT claims via `HttpContextAccessor`
- OpenTelemetry + Zipkin tracing + Azure Monitor integration
- JSON serialization uses `System.Text.Json` with `JsonStringEnumConverter`

## Database Patterns

- SQL Server via EF Core
- Entities: `Order`, `OrderItem`, `OrderReturn`, `Event` (outbox pattern)
- `Event` table stores published events as JSON for audit/replay
- Code-first migrations in `OrderService.Api/Migrations/`
- Connection string resolved via Dapr secrets store or `appsettings.json`
- Retry on transient failure enabled (`EnableRetryOnFailure`)

## Key Patterns

- **Embedded consumer**: `BackgroundService` subscribes to Dapr topics via HTTP endpoints
- **Messaging abstraction**: `IMessagingProvider` with Dapr and RabbitMQ implementations
- **Event outbox**: Events stored in `Event` table before publishing
- Order statuses: `Pending` → `Confirmed` → `Processing` → `Shipped` → `Delivered` / `Cancelled`
- Return workflow: `ReturnRequested` → `ReturnApproved` → `ReturnReceived` → `Refunded`

## Testing Requirements

- All new controllers MUST have unit tests
- All new services MUST have unit tests
- Use **xUnit** + **Moq** + **FluentAssertions** as the test framework
- Mock repositories and messaging providers in unit tests
- Do NOT call real SQL Server or downstream services in unit tests
- Run: `dotnet test`
- Project: `OrderService.Tests/`

## Dapr Integration

- **Pub/Sub Publisher**: Publishes `order.created`, `order.status.changed`, `order.cancelled`, `return.*` events
- **Pub/Sub Consumer**: Embedded BackgroundService listens to subscription endpoints
- **Secrets Store**: Connection strings fetched from Dapr secrets store
- **Ports**: Dapr HTTP 3500, Dapr gRPC 50001

## Security Rules

- JWT Bearer token MUST be validated via ASP.NET Core Authentication before accessing any endpoint
- Use `ICurrentUserService` to extract user identity from JWT claims — never trust client-provided user IDs
- Dapr subscription endpoints are authenticated by the Dapr sidecar — no additional JWT required
- Validate all request bodies using **FluentValidation** validators before reaching service logic
- Sanitize all inputs
- Never expose internal order IDs or EF Core entity structures in API responses

## Error Handling Contract

All errors MUST follow this JSON structure:

```json
{
  "error": {
    "code": "STRING_CODE",
    "message": "Human readable message",
    "correlationId": "uuid"
  }
}
```

- Never expose stack traces in production
- Use centralized exception middleware or `IExceptionHandler` only

## Logging Rules

- Use structured JSON logging via **Serilog**
- Include:
  - timestamp
  - level
  - serviceName
  - correlationId
  - message
- Never log JWT tokens
- Never log secrets or connection strings

## Non-Goals

- This service does NOT handle payment processing — handled by payment-service
- This service does NOT orchestrate the order fulfillment saga — handled by order-processor-service
- This service does NOT manage product catalog or inventory
- This service does NOT handle authentication or JWT issuance

## Environment Variables

```
PORT=8006
ASPNETCORE_ENVIRONMENT=Development
ConnectionStrings__DefaultConnection=Server=localhost,1434;Database=OrderServiceDb;User Id=sa;Password=Admin123!;TrustServerCertificate=True
Jwt__Key=<shared-secret>
Jwt__Issuer=xshopai
Jwt__Audience=xshopai
DAPR_HTTP_PORT=3500
```

## Common Commands

```bash
dotnet run --project OrderService.Api       # Run service
dotnet ef migrations add <Name> --project OrderService.Api  # Add migration
dotnet ef database update --project OrderService.Api        # Apply migrations
dotnet test                                                  # Run tests
dotnet build                                                 # Build solution
```
