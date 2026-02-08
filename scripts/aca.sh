#!/bin/bash
# ============================================================================
# Azure Container Apps Deployment Script for Order Service
# ============================================================================
# PREREQUISITE: Run infrastructure deployment first:
#   cd infrastructure/azure/aca/scripts && ./deploy.sh
# ============================================================================

set -e

# ============================================================================
# CONFIGURATION - Edit these variables as needed
# ============================================================================

# Service Configuration
SERVICE_NAME="order-service"
APP_PORT=8006
PROJECT_NAME="xshopai"

# Database Configuration
DB_NAME="order_service_db"

# Container Resources (Production-level)
CPU="1.0"
MEMORY="2.0Gi"
MIN_REPLICAS=1
MAX_REPLICAS=10

# Dapr Configuration (fixed for Azure Container Apps)
DAPR_HTTP_PORT=3500
DAPR_GRPC_PORT=50001

# ============================================================================
# COLORS & HELPER FUNCTIONS
# ============================================================================
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
CYAN='\033[0;36m'
NC='\033[0m'

print_header() { echo -e "\n${BLUE}=== $1 ===${NC}\n"; }
print_success() { echo -e "${GREEN}✓ $1${NC}"; }
print_warning() { echo -e "${YELLOW}⚠ $1${NC}"; }
print_error() { echo -e "${RED}✗ $1${NC}"; }
print_info() { echo -e "${CYAN}ℹ $1${NC}"; }

# ============================================================================
# PREREQUISITES CHECK
# ============================================================================
print_header "Checking Prerequisites"

command -v az &>/dev/null || { print_error "Azure CLI not installed"; exit 1; }
print_success "Azure CLI installed"

command -v docker &>/dev/null || { print_error "Docker not installed"; exit 1; }
print_success "Docker installed"

az account show &>/dev/null || az login
print_success "Logged into Azure"

# Get script and service directories
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SERVICE_DIR="$(dirname "$SCRIPT_DIR")"

# ============================================================================
# USER INPUT - Environment & Suffix
# ============================================================================
print_header "Environment Selection"

echo "Available environments: dev, prod"
read -p "Enter environment [dev]: " ENVIRONMENT
ENVIRONMENT="${ENVIRONMENT:-dev}"

[[ "$ENVIRONMENT" =~ ^(dev|prod)$ ]] || { print_error "Invalid environment (dev/prod only)"; exit 1; }
print_success "Environment: $ENVIRONMENT"

echo ""
echo "Find your suffix by running:"
echo -e "  ${BLUE}az group list --query \"[?starts_with(name, 'rg-xshopai-$ENVIRONMENT')].name\" -o tsv${NC}"
echo ""
read -p "Enter infrastructure suffix: " SUFFIX

[[ "$SUFFIX" =~ ^[a-z0-9]{3,6}$ ]] || { print_error "Invalid suffix (3-6 lowercase alphanumeric)"; exit 1; }
print_success "Suffix: $SUFFIX"

# ============================================================================
# DERIVED RESOURCE NAMES (must match infrastructure deployment)
# ============================================================================
RESOURCE_GROUP="rg-${PROJECT_NAME}-${ENVIRONMENT}-${SUFFIX}"
ACR_NAME="${PROJECT_NAME}${ENVIRONMENT}${SUFFIX}"
CONTAINER_ENV="cae-${PROJECT_NAME}-${ENVIRONMENT}-${SUFFIX}"
CONTAINER_APP_NAME="ca-${SERVICE_NAME}-${ENVIRONMENT}-${SUFFIX}"
SQL_SERVER="sql-${PROJECT_NAME}-${ENVIRONMENT}-${SUFFIX}"
KEY_VAULT="kv-${PROJECT_NAME}-${ENVIRONMENT}-${SUFFIX}"
MANAGED_IDENTITY="id-${PROJECT_NAME}-${ENVIRONMENT}-${SUFFIX}"

# ============================================================================
# VERIFY INFRASTRUCTURE EXISTS
# ============================================================================
print_header "Verifying Infrastructure"

az group show --name "$RESOURCE_GROUP" &>/dev/null || { print_error "Resource group not found: $RESOURCE_GROUP"; exit 1; }
print_success "Resource Group: $RESOURCE_GROUP"

ACR_LOGIN_SERVER=$(az acr show --name "$ACR_NAME" --query loginServer -o tsv 2>/dev/null) || { print_error "ACR not found: $ACR_NAME"; exit 1; }
print_success "Container Registry: $ACR_LOGIN_SERVER"

az containerapp env show --name "$CONTAINER_ENV" --resource-group "$RESOURCE_GROUP" &>/dev/null || { print_error "Container Env not found: $CONTAINER_ENV"; exit 1; }
print_success "Container Environment: $CONTAINER_ENV"

SQL_HOST=$(az sql server show --name "$SQL_SERVER" --resource-group "$RESOURCE_GROUP" --query fullyQualifiedDomainName -o tsv 2>/dev/null) || { print_error "SQL Server not found: $SQL_SERVER"; exit 1; }
print_success "SQL Server: $SQL_HOST"

# Get Managed Identity (optional)
IDENTITY_ID=$(MSYS_NO_PATHCONV=1 az identity show --name "$MANAGED_IDENTITY" --resource-group "$RESOURCE_GROUP" --query id -o tsv 2>/dev/null || echo "")
IDENTITY_CLIENT_ID=$(az identity show --name "$MANAGED_IDENTITY" --resource-group "$RESOURCE_GROUP" --query clientId -o tsv 2>/dev/null || echo "")
[ -n "$IDENTITY_ID" ] && print_success "Managed Identity: $MANAGED_IDENTITY" || print_warning "Managed Identity not found (optional)"

# ============================================================================
# DATABASE SETUP
# ============================================================================
print_header "Database Configuration"

# Create database if not exists
if az sql db show --resource-group "$RESOURCE_GROUP" --server "$SQL_SERVER" --name "$DB_NAME" &>/dev/null; then
    print_success "Database exists: $DB_NAME"
else
    print_info "Creating database: $DB_NAME"
    az sql db create \
        --resource-group "$RESOURCE_GROUP" \
        --server "$SQL_SERVER" \
        --name "$DB_NAME" \
        --edition Basic \
        --capacity 5 \
        --max-size 2GB \
        --output none
    print_success "Database created: $DB_NAME"
fi

# ============================================================================
# CONFIRMATION
# ============================================================================
print_header "Deployment Summary"

echo "Environment:        $ENVIRONMENT"
echo "Resource Group:     $RESOURCE_GROUP"
echo "Container App:      $CONTAINER_APP_NAME"
echo "Container Name:     $SERVICE_NAME"
echo "Image:              $ACR_LOGIN_SERVER/$SERVICE_NAME:latest"
echo "SQL Server:         $SQL_HOST"
echo "Database:           $DB_NAME"
echo "CPU/Memory:         $CPU / $MEMORY"
echo "Replicas:           $MIN_REPLICAS - $MAX_REPLICAS"
echo ""

# ============================================================================
# BUILD & PUSH IMAGE
# ============================================================================
print_header "Building and Pushing Image"

az acr login --name "$ACR_NAME"
cd "$SERVICE_DIR"

IMAGE_TAG="$ACR_LOGIN_SERVER/$SERVICE_NAME:latest"
docker build --target production -t "$SERVICE_NAME:latest" .
docker tag "$SERVICE_NAME:latest" "$IMAGE_TAG"
docker push "$IMAGE_TAG"
print_success "Image pushed: $IMAGE_TAG"

# ============================================================================
# DEPLOY CONTAINER APP
# ============================================================================
print_header "Deploying Container App"

ACR_PASSWORD=$(az acr credential show --name "$ACR_NAME" --query "passwords[0].value" -o tsv)

# Map environment to app config (dev->Development, prod->Production)
ASPNETCORE_ENV="Development"
LOG_LEVEL="Information"
[ "$ENVIRONMENT" = "prod" ] && ASPNETCORE_ENV="Production" && LOG_LEVEL="Warning"

# Retrieve secrets from Key Vault
# All secrets are fetched at deployment time and set as env vars
# This avoids race conditions with Dapr sidecar startup
print_info "Retrieving secrets from Key Vault..."

# Per-service Application Insights (each service has its own App Insights resource)
APP_INSIGHTS_CONN=$(az keyvault secret show --vault-name "$KEY_VAULT" --name "appinsights-order-service" --query "value" -o tsv 2>/dev/null || echo "")
[ -n "$APP_INSIGHTS_CONN" ] && print_success "  appinsights-order-service: retrieved" || print_warning "  appinsights-order-service: not configured (telemetry disabled)"

# JWT secret
JWT_SECRET=$(az keyvault secret show --vault-name "$KEY_VAULT" --name "jwt-secret" --query "value" -o tsv 2>/dev/null || echo "")
[ -n "$JWT_SECRET" ] && print_success "  jwt-secret: retrieved" || print_error "  jwt-secret: NOT FOUND"

# SQL Server connection
SQL_CONNECTION=$(az keyvault secret show --vault-name "$KEY_VAULT" --name "sql-server-connection" --query "value" -o tsv 2>/dev/null || echo "")
if [ -n "$SQL_CONNECTION" ]; then
    # Append database name to connection string
    SQL_CONNECTION="${SQL_CONNECTION};Database=$DB_NAME"
    print_success "  sql-server-connection: retrieved"
else
    # Fallback to Azure AD auth
    SQL_CONNECTION="Server=$SQL_HOST;Database=$DB_NAME;Authentication=Active Directory Default;TrustServerCertificate=True;Encrypt=True"
    print_warning "  sql-server-connection: using Azure AD fallback"
fi

# Service tokens (order-service calls these services)
SVC_CART_TOKEN=$(az keyvault secret show --vault-name "$KEY_VAULT" --name "service-cart-token" --query "value" -o tsv 2>/dev/null || echo "")
SVC_INVENTORY_TOKEN=$(az keyvault secret show --vault-name "$KEY_VAULT" --name "service-inventory-token" --query "value" -o tsv 2>/dev/null || echo "")
SVC_PAYMENT_TOKEN=$(az keyvault secret show --vault-name "$KEY_VAULT" --name "service-payment-token" --query "value" -o tsv 2>/dev/null || echo "")
SVC_PRODUCT_TOKEN=$(az keyvault secret show --vault-name "$KEY_VAULT" --name "service-product-token" --query "value" -o tsv 2>/dev/null || echo "")
SVC_USER_TOKEN=$(az keyvault secret show --vault-name "$KEY_VAULT" --name "service-user-token" --query "value" -o tsv 2>/dev/null || echo "")
print_success "  service-*-token: retrieved"

# Environment variables for the container (sorted alphabetically)
# All secrets are set as env vars - no Dapr secretstore access needed at runtime
ENV_VARS=(
    "APPLICATIONINSIGHTS_CONNECTION_STRING=$APP_INSIGHTS_CONN"
    "ASPNETCORE_ENVIRONMENT=$ASPNETCORE_ENV"
    "ASPNETCORE_URLS=http://+:$APP_PORT"
    "AZURE_CLIENT_ID=$IDENTITY_CLIENT_ID"
    "Dapr__Enabled=true"
    "Dapr__HttpPort=$DAPR_HTTP_PORT"
    "Dapr__PubSubName=pubsub"
    "DATABASE_CONNECTION_STRING=$SQL_CONNECTION"
    "JWT_SECRET=$JWT_SECRET"
    "Logging__LogLevel__Default=$LOG_LEVEL"
    "MESSAGING_PROVIDER=dapr"
    "OTEL_RESOURCE_ATTRIBUTES=service.version=1.0.0,service.namespace=$PROJECT_NAME,deployment.environment=$ENVIRONMENT"
    "OTEL_SERVICE_NAME=$SERVICE_NAME"
    "SERVICE_CART_TOKEN=$SVC_CART_TOKEN"
    "SERVICE_INVENTORY_TOKEN=$SVC_INVENTORY_TOKEN"
    "SERVICE_PAYMENT_TOKEN=$SVC_PAYMENT_TOKEN"
    "SERVICE_PRODUCT_TOKEN=$SVC_PRODUCT_TOKEN"
    "SERVICE_USER_TOKEN=$SVC_USER_TOKEN"
)

if az containerapp show --name "$CONTAINER_APP_NAME" --resource-group "$RESOURCE_GROUP" &>/dev/null; then
    print_info "Updating existing container app..."
    az containerapp update \
        --name "$CONTAINER_APP_NAME" \
        --resource-group "$RESOURCE_GROUP" \
        --image "$IMAGE_TAG" \
        --set-env-vars "${ENV_VARS[@]}" \
        --output none
else
    print_info "Creating new container app..."
    MSYS_NO_PATHCONV=1 az containerapp create \
        --name "$CONTAINER_APP_NAME" \
        --container-name "$SERVICE_NAME" \
        --resource-group "$RESOURCE_GROUP" \
        --environment "$CONTAINER_ENV" \
        --image "$IMAGE_TAG" \
        --registry-server "$ACR_LOGIN_SERVER" \
        --registry-username "$ACR_NAME" \
        --registry-password "$ACR_PASSWORD" \
        --target-port "$APP_PORT" \
        --ingress external \
        --min-replicas "$MIN_REPLICAS" \
        --max-replicas "$MAX_REPLICAS" \
        --cpu "$CPU" \
        --memory "$MEMORY" \
        --enable-dapr \
        --dapr-app-id "$SERVICE_NAME" \
        --dapr-app-port "$APP_PORT" \
        --env-vars "${ENV_VARS[@]}" \
        ${IDENTITY_ID:+--user-assigned "$IDENTITY_ID"} \
        --tags "project=$PROJECT_NAME" "environment=$ENVIRONMENT" "suffix=$SUFFIX" "service=$SERVICE_NAME" \
        --output none
fi
print_success "Container app deployed"

# ============================================================================
# VERIFY DEPLOYMENT
# ============================================================================
print_header "Verifying Deployment"

APP_URL=$(az containerapp show --name "$CONTAINER_APP_NAME" --resource-group "$RESOURCE_GROUP" --query properties.configuration.ingress.fqdn -o tsv)

echo ""
echo -e "${GREEN}✅ DEPLOYMENT SUCCESSFUL${NC}"
echo ""
echo "Application URL:  https://$APP_URL"
echo "Health Check:     https://$APP_URL/health"
echo "Swagger UI:       https://$APP_URL/swagger"
echo ""
echo "Useful commands:"
echo -e "  Logs:      ${BLUE}az containerapp logs show --name $CONTAINER_APP_NAME --resource-group $RESOURCE_GROUP --follow${NC}"
echo -e "  Dapr logs: ${BLUE}az containerapp logs show --name $CONTAINER_APP_NAME --resource-group $RESOURCE_GROUP --container daprd --follow${NC}"
echo -e "  Delete:    ${BLUE}az containerapp delete --name $CONTAINER_APP_NAME --resource-group $RESOURCE_GROUP --yes${NC}"
echo ""

# Optional: Test health endpoint
print_info "Waiting 15s for app to start..."
sleep 15
HTTP_STATUS=$(curl -s -o /dev/null -w "%{http_code}" --max-time 30 "https://$APP_URL/health" 2>/dev/null || echo "000")
[ "$HTTP_STATUS" = "200" ] && print_success "Health check passed!" || print_warning "Health check returned HTTP $HTTP_STATUS (app may still be starting)"
