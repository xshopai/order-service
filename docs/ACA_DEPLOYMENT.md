# Order Service - Azure Container Apps Deployment

## Overview

This guide covers deploying the Order Service (.NET 8) to Azure Container Apps (ACA) with:

- **Dapr integration** for event-driven order management
- **Azure SQL Server** with Azure AD authentication (managed identity)
- **Automatic database migrations** at startup

## Architecture

```
┌─────────────────────────────────────────────────────────────────────┐
│                     Azure Container Apps Environment                │
│  ┌─────────────────────────────────────────────────────────────┐   │
│  │                      order-service                           │   │
│  │  ┌─────────────────┐    ┌─────────────────┐                │   │
│  │  │   .NET 8 App    │────│   Dapr Sidecar   │                │   │
│  │  │   (Port 8006)   │    │   (Port 3500)    │                │   │
│  │  └────────┬────────┘    └────────┬─────────┘                │   │
│  └───────────│───────────────────────│──────────────────────────┘   │
│              │                       │                              │
│              │ Azure AD Auth         │ Pub/Sub Events               │
│              │ (AZURE_CLIENT_ID)     │                              │
│              ▼                       ▼                              │
│  ┌───────────────────┐    ┌─────────────────┐                      │
│  │   Azure SQL DB    │    │  Service Bus    │                      │
│  │ order_service_db  │    │    (pubsub)     │                      │
│  └───────────────────┘    └─────────────────┘                      │
└─────────────────────────────────────────────────────────────────────┘
```

## Prerequisites

- Azure CLI installed and authenticated
- Docker installed
- .NET 8 SDK installed
- Azure subscription with appropriate permissions
- **Infrastructure deployed** via `deploy-infra.sh` (creates ACR, SQL Server, Managed Identity, etc.)

## Quick Deployment

### Step 1: Deploy Infrastructure (if not done)

```bash
cd infrastructure/azure/aca/scripts
./deploy-infra.sh
# Note the suffix (e.g., "74f9")
```

### Step 2: Deploy Order Service

**PowerShell (Windows):**

```powershell
cd order-service/scripts
.\aca.ps1
```

**Bash (macOS/Linux/Git Bash):**

```bash
cd order-service/scripts
./aca.sh
```

## Authentication: Azure AD with Managed Identity

Order Service uses **Azure AD authentication** to connect to SQL Server, not SQL username/password. This is required for Azure subscriptions with MCAPS (Microsoft Secure Future Initiative) policies.

### How It Works

1. **Managed Identity**: `id-xshopai-{env}-{suffix}` is created during infrastructure deployment
2. **SQL Permissions**: The deployment script grants the managed identity SQL roles:
   - `db_datareader` - Read data
   - `db_datawriter` - Write data
   - `db_ddladmin` - Create/modify tables (for EF Core migrations)
3. **AZURE_CLIENT_ID**: The container app gets this env var to tell `DefaultAzureCredential` which identity to use
4. **Connection String**: Uses `Authentication=Active Directory Default` (no password)

### Connection String Format

```
Server=sql-xshopai-dev-74f9.database.windows.net;
Database=order_service_db;
Authentication=Active Directory Default;
TrustServerCertificate=True;
Encrypt=True
```

### Required Environment Variables

| Variable                    | Description                              | Example                                |
| --------------------------- | ---------------------------------------- | -------------------------------------- |
| `AZURE_CLIENT_ID`           | Client ID of the managed identity        | `5d11b916-5eb4-4f93-9ffa-a54872152892` |
| `database_connectionString` | SQL connection string with Azure AD auth | See above                              |
| `jwt_secret`                | JWT signing secret (from Key Vault)      | `se+DhS0POx...`                        |
| `jwt_issuer`                | JWT issuer                               | `auth-service`                         |
| `jwt_audience`              | JWT audience                             | `xshopai-platform`                     |

## Database Migrations

EF Core migrations run **automatically at startup** with retry logic:

- 5 retry attempts
- 5 second delay between retries
- Handles Dapr sidecar timing issues

### Migration Flow

```
App Start
    │
    ▼
Wait for Dapr sidecar (if needed)
    │
    ▼
Get connection string (Dapr Secret Store → env fallback)
    │
    ▼
Run MigrateAsync() with retry loop
    │
    ▼
Tables created: Orders, OrderItems, __EFMigrationsHistory
```

### Manual Migration (Development)

```bash
cd OrderService.Api
dotnet ef migrations add <MigrationName>
dotnet ef database update
```

## Manual SQL Permission Setup

If the automated SQL permission setup fails, run these commands manually:

### Using Azure Portal Query Editor

```sql
-- Create user from managed identity
CREATE USER [id-xshopai-dev-74f9] FROM EXTERNAL PROVIDER;

-- Grant roles
ALTER ROLE db_datareader ADD MEMBER [id-xshopai-dev-74f9];
ALTER ROLE db_datawriter ADD MEMBER [id-xshopai-dev-74f9];
ALTER ROLE db_ddladmin ADD MEMBER [id-xshopai-dev-74f9];
```

### Using PowerShell with Access Token

```powershell
$token = az account get-access-token --resource https://database.windows.net --query accessToken -o tsv

Invoke-Sqlcmd `
    -ServerInstance "sql-xshopai-dev-74f9.database.windows.net" `
    -Database "order_service_db" `
    -AccessToken $token `
    -Query "CREATE USER [id-xshopai-dev-74f9] FROM EXTERNAL PROVIDER; ALTER ROLE db_datareader ADD MEMBER [id-xshopai-dev-74f9]; ALTER ROLE db_datawriter ADD MEMBER [id-xshopai-dev-74f9]; ALTER ROLE db_ddladmin ADD MEMBER [id-xshopai-dev-74f9];"
```

## API Endpoints

### Operational Endpoints (External)

| Endpoint            | Description                |
| ------------------- | -------------------------- |
| `GET /health`       | Overall service health     |
| `GET /health/ready` | Kubernetes readiness probe |
| `GET /health/live`  | Kubernetes liveness probe  |
| `GET /metrics`      | Prometheus metrics         |

### Business Endpoints

| Endpoint                  | Description             |
| ------------------------- | ----------------------- |
| `GET /api/orders`         | List orders (paginated) |
| `GET /api/orders/{id}`    | Get order by ID         |
| `POST /api/orders`        | Create order            |
| `PUT /api/orders/{id}`    | Update order            |
| `DELETE /api/orders/{id}` | Cancel/delete order     |

## Monitoring

### View Logs

```bash
az containerapp logs show \
    --name order-service \
    --resource-group rg-xshopai-dev-74f9 \
    --type console \
    --tail 100
```

### Check Migration Status

```bash
az containerapp logs show \
    --name order-service \
    --resource-group rg-xshopai-dev-74f9 \
    --type console \
    --tail 200 | grep -i "migration"
```

### Verify Tables Created

```powershell
$token = az account get-access-token --resource https://database.windows.net --query accessToken -o tsv
Invoke-Sqlcmd -ServerInstance "sql-xshopai-dev-74f9.database.windows.net" -Database "order_service_db" -AccessToken $token -Query "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES"
```

## Troubleshooting

### Migration Fails with "ManagedIdentityCredential authentication failed"

**Cause**: `AZURE_CLIENT_ID` environment variable not set or incorrect.

**Fix**:

```bash
# Get the managed identity client ID
az identity show --name id-xshopai-dev-74f9 --resource-group rg-xshopai-dev-74f9 --query clientId -o tsv

# Update the container app
az containerapp update \
    --name order-service \
    --resource-group rg-xshopai-dev-74f9 \
    --set-env-vars "AZURE_CLIENT_ID=<client-id>"
```

### Migration Fails with "Login failed for user"

**Cause**: Managed identity doesn't have SQL permissions.

**Fix**: Run the SQL permission commands manually (see "Manual SQL Permission Setup" above).

### Database Connection Timeout

**Cause**: SQL Server firewall blocking connection.

**Fix**: Add firewall rule or ensure Container Apps subnet is allowed:

```bash
az sql server firewall-rule create \
    --resource-group rg-xshopai-dev-74f9 \
    --server sql-xshopai-dev-74f9 \
    --name AllowAzureServices \
    --start-ip-address 0.0.0.0 \
    --end-ip-address 0.0.0.0
```

### Dapr Sidecar Connection Errors

These are expected during startup. The retry logic handles timing issues:

```
[WRN]: Failed to retrieve secret 'jwt:secret' from Dapr, trying configuration fallback
```

The service falls back to environment variables when Dapr isn't ready.

## Resource Naming Convention

| Resource Type    | Name Pattern                 | Example                |
| ---------------- | ---------------------------- | ---------------------- |
| Resource Group   | `rg-xshopai-{env}-{suffix}`  | `rg-xshopai-dev-74f9`  |
| SQL Server       | `sql-xshopai-{env}-{suffix}` | `sql-xshopai-dev-74f9` |
| Database         | `order_service_db`           | `order_service_db`     |
| Managed Identity | `id-xshopai-{env}-{suffix}`  | `id-xshopai-dev-74f9`  |
| Container App    | `order-service`              | `order-service`        |
| Container Env    | `cae-xshopai-{env}-{suffix}` | `cae-xshopai-dev-74f9` |
