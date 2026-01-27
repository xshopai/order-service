# Order Service - Azure Container Apps Deployment

## Overview

This guide covers deploying the Order Service (.NET 8) to Azure Container Apps (ACA) with Dapr integration for event-driven order management.

## Prerequisites

- Azure CLI installed and authenticated
- Docker installed
- .NET 8 SDK installed
- Azure subscription with appropriate permissions
- Azure Container Registry (ACR) created
- Azure PostgreSQL Flexible Server

## Quick Deployment

### Using the Deployment Script

**PowerShell (Windows):**

```powershell
cd scripts
.\aca.ps1
```

**Bash (macOS/Linux):**

```bash
cd scripts
./aca.sh
```

## Manual Deployment

### 1. Set Variables

```bash
RESOURCE_GROUP="rg-xshopai-aca"
LOCATION="swedencentral"
ACR_NAME="acrxshopaiaca"
ENVIRONMENT_NAME="cae-xshopai-aca"
POSTGRES_SERVER="psql-xshopai-aca"
APP_NAME="order-service"
APP_PORT=1006
DATABASE_NAME="orders_db"
```

### 2. Create PostgreSQL Database

```bash
az postgres flexible-server create \
  --name $POSTGRES_SERVER \
  --resource-group $RESOURCE_GROUP \
  --location $LOCATION \
  --admin-user pgadmin \
  --admin-password <password> \
  --sku-name Standard_B1ms \
  --tier Burstable \
  --storage-size 32

az postgres flexible-server db create \
  --server-name $POSTGRES_SERVER \
  --resource-group $RESOURCE_GROUP \
  --database-name $DATABASE_NAME
```

### 3. Build and Push Image

```bash
# Publish .NET application
dotnet publish OrderService.Api/OrderService.Api.csproj -c Release -o ./publish

# Login to ACR
az acr login --name $ACR_NAME

# Build and push Docker image
docker build -t $ACR_NAME.azurecr.io/$APP_NAME:latest .
docker push $ACR_NAME.azurecr.io/$APP_NAME:latest
```

### 4. Deploy Container App

```bash
CONNECTION_STRING="Host=${POSTGRES_SERVER}.postgres.database.azure.com;Database=${DATABASE_NAME};Username=pgadmin;Password=<password>;SSL Mode=Require"

az containerapp create \
  --name $APP_NAME \
  --resource-group $RESOURCE_GROUP \
  --environment $ENVIRONMENT_NAME \
  --image $ACR_NAME.azurecr.io/$APP_NAME:latest \
  --registry-server $ACR_NAME.azurecr.io \
  --target-port $APP_PORT \
  --ingress internal \
  --min-replicas 1 \
  --max-replicas 5 \
  --cpu 0.5 \
  --memory 1Gi \
  --enable-dapr \
  --dapr-app-id $APP_NAME \
  --dapr-app-port $APP_PORT \
  --secrets "db-conn=$CONNECTION_STRING" \
  --env-vars \
    "ASPNETCORE_URLS=http://+:$APP_PORT" \
    "ASPNETCORE_ENVIRONMENT=Production" \
    "ConnectionStrings__DefaultConnection=secretref:db-conn"
```

## Configuration

### Environment Variables

| Variable                               | Description                  |
| -------------------------------------- | ---------------------------- |
| `ASPNETCORE_URLS`                      | ASP.NET Core URLs            |
| `ASPNETCORE_ENVIRONMENT`               | Environment (Production)     |
| `ConnectionStrings__DefaultConnection` | PostgreSQL connection string |

## API Endpoints

- `GET /api/orders` - List orders
- `GET /api/orders/{id}` - Get order by ID
- `POST /api/orders` - Create order
- `PUT /api/orders/{id}` - Update order
- `DELETE /api/orders/{id}` - Delete order

## Monitoring

```bash
az containerapp logs show \
  --name $APP_NAME \
  --resource-group $RESOURCE_GROUP \
  --follow
```

## Troubleshooting

### Database Connection Issues

1. Verify PostgreSQL firewall settings
2. Check connection string format
3. Ensure SSL is enabled
