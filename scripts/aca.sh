#!/bin/bash
# Azure Container Apps Deployment Script for Order Service (.NET 8)
set -e

RED='\033[0;31m'; GREEN='\033[0;32m'; BLUE='\033[0;34m'; NC='\033[0m'
print_header() { echo -e "\n${BLUE}============================================================================${NC}\n${BLUE}$1${NC}\n${BLUE}============================================================================${NC}\n"; }
print_success() { echo -e "${GREEN}✓ $1${NC}"; }

prompt_with_default() { local prompt="$1" default="$2" varname="$3"; read -p "$prompt [$default]: " input; eval "$varname=\"${input:-$default}\""; }

print_header "Checking Prerequisites"
command -v az &> /dev/null || { echo "Azure CLI not installed"; exit 1; }
command -v docker &> /dev/null || { echo "Docker not installed"; exit 1; }
az account show &> /dev/null || az login
print_success "Prerequisites verified"

print_header "Azure Configuration"
prompt_with_default "Enter Resource Group name" "rg-xshopai-aca" RESOURCE_GROUP
prompt_with_default "Enter Azure Location" "swedencentral" LOCATION
prompt_with_default "Enter Azure Container Registry name" "acrxshopaiaca" ACR_NAME
prompt_with_default "Enter Container Apps Environment name" "cae-xshopai-aca" ENVIRONMENT_NAME
prompt_with_default "Enter PostgreSQL Server name" "psql-xshopai-aca" POSTGRES_SERVER
prompt_with_default "Enter PostgreSQL Password" "" POSTGRES_PASSWORD

APP_NAME="order-service"
APP_PORT=1006

read -p "Proceed with deployment? (y/N): " CONFIRM
[[ ! "$CONFIRM" =~ ^[Yy]$ ]] && exit 0

print_header "Setting Up PostgreSQL"
if ! az postgres flexible-server show --name "$POSTGRES_SERVER" --resource-group "$RESOURCE_GROUP" &> /dev/null; then
    az postgres flexible-server create \
        --name "$POSTGRES_SERVER" \
        --resource-group "$RESOURCE_GROUP" \
        --location "$LOCATION" \
        --admin-user orderadmin \
        --admin-password "$POSTGRES_PASSWORD" \
        --sku-name Standard_B1ms \
        --output none
fi

az postgres flexible-server db create \
    --resource-group "$RESOURCE_GROUP" \
    --server-name "$POSTGRES_SERVER" \
    --database-name order_db \
    --output none 2>/dev/null || true

POSTGRES_HOST="${POSTGRES_SERVER}.postgres.database.azure.com"
CONNECTION_STRING="Host=${POSTGRES_HOST};Port=5432;Database=order_db;Username=orderadmin;Password=${POSTGRES_PASSWORD};SSL Mode=Require"

print_header "Building and Deploying"
ACR_LOGIN_SERVER=$(az acr show --name "$ACR_NAME" --query loginServer -o tsv)
az acr login --name "$ACR_NAME"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$(dirname "$SCRIPT_DIR")"

IMAGE_TAG="${ACR_LOGIN_SERVER}/${APP_NAME}:latest"
docker build -t "$IMAGE_TAG" .
docker push "$IMAGE_TAG"

az containerapp env show --name "$ENVIRONMENT_NAME" --resource-group "$RESOURCE_GROUP" &> /dev/null || \
    az containerapp env create --name "$ENVIRONMENT_NAME" --resource-group "$RESOURCE_GROUP" --location "$LOCATION" --output none

if az containerapp show --name "$APP_NAME" --resource-group "$RESOURCE_GROUP" &> /dev/null; then
    az containerapp update --name "$APP_NAME" --resource-group "$RESOURCE_GROUP" --image "$IMAGE_TAG" --output none
else
    az containerapp create \
        --name "$APP_NAME" \
        --resource-group "$RESOURCE_GROUP" \
        --environment "$ENVIRONMENT_NAME" \
        --image "$IMAGE_TAG" \
        --registry-server "$ACR_LOGIN_SERVER" \
        --target-port $APP_PORT \
        --ingress internal \
        --min-replicas 1 \
        --max-replicas 5 \
        --cpu 0.5 \
        --memory 1Gi \
        --enable-dapr \
        --dapr-app-id "$APP_NAME" \
        --dapr-app-port $APP_PORT \
        --secrets "db-connection=${CONNECTION_STRING}" \
        --env-vars \
            "ASPNETCORE_ENVIRONMENT=Production" \
            "ASPNETCORE_URLS=http://+:$APP_PORT" \
            "ConnectionStrings__DefaultConnection=secretref:db-connection" \
            "Dapr__HttpPort=3500" \
            "Dapr__PubSubName=xshopai-pubsub" \
        --output none
fi

print_header "Deployment Complete!"
echo -e "${GREEN}Order Service deployed!${NC} Dapr App ID: $APP_NAME"
