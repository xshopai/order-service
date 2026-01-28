#!/bin/bash

# ============================================================================
# Azure Container Apps Deployment Script for Order Service
# ============================================================================
# This script deploys the Order Service to Azure Container Apps.
# 
# PREREQUISITE: Run the infrastructure deployment script first:
#   cd infrastructure/azure/aca/scripts
#   ./deploy-infra.sh
#
# The infrastructure script creates all shared resources:
#   - Resource Group, ACR, Container Apps Environment
#   - Service Bus, Redis, Cosmos DB, MySQL, SQL Server, Key Vault
#   - Dapr components (pubsub, statestore, secretstore)
#
# This script will:
#   1. Build and push the Docker image
#   2. Create the SQL database if it doesn't exist
#   3. Deploy the container app with Dapr sidecar
# ============================================================================

set -e

# -----------------------------------------------------------------------------
# Colors for output
# -----------------------------------------------------------------------------
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
CYAN='\033[0;36m'
NC='\033[0m' # No Color

# Print functions
print_header() {
    echo -e "\n${BLUE}==============================================================================${NC}"
    echo -e "${BLUE}$1${NC}"
    echo -e "${BLUE}==============================================================================${NC}\n"
}

print_success() {
    echo -e "${GREEN}✓ $1${NC}"
}

print_warning() {
    echo -e "${YELLOW}⚠ $1${NC}"
}

print_error() {
    echo -e "${RED}✗ $1${NC}"
}

print_info() {
    echo -e "${CYAN}ℹ $1${NC}"
}

# ============================================================================
# Prerequisites Check
# ============================================================================
print_header "Checking Prerequisites"

# Check Azure CLI
if ! command -v az &> /dev/null; then
    print_error "Azure CLI is not installed. Please install it from: https://docs.microsoft.com/en-us/cli/azure/install-azure-cli"
    exit 1
fi
print_success "Azure CLI is installed"

# Check Docker
if ! command -v docker &> /dev/null; then
    print_error "Docker is not installed. Please install Docker first."
    exit 1
fi
print_success "Docker is installed"

# Check .NET SDK (for migrations)
if ! command -v dotnet &> /dev/null; then
    print_warning "dotnet SDK is not installed. Database migrations will be skipped."
    DOTNET_AVAILABLE=false
else
    print_success ".NET SDK is installed"
    DOTNET_AVAILABLE=true
fi

# Check if logged into Azure
if ! az account show &> /dev/null; then
    print_warning "Not logged into Azure. Initiating login..."
    az login
fi
print_success "Logged into Azure"

# ============================================================================
# Configuration
# ============================================================================
print_header "Configuration"

# Service-specific configuration
SERVICE_NAME="order-service"
SERVICE_VERSION="1.0.0"
APP_PORT=8006
PROJECT_NAME="xshopai"
DATABASE_NAME="order_service_db"

# Dapr configuration (per PORT_CONFIGURATION.md: order-service = 3506/50006)
DAPR_HTTP_PORT=3506
DAPR_GRPC_PORT=50006
DAPR_PUBSUB_NAME="pubsub"

# Get script directory and service directory
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SERVICE_DIR="$(dirname "$SCRIPT_DIR")"

# ============================================================================
# Environment Selection
# ============================================================================
echo -e "${CYAN}Available Environments:${NC}"
echo "   dev     - Development environment"
echo "   staging - Staging/QA environment"
echo "   prod    - Production environment"
echo ""

read -p "Enter environment (dev/staging/prod) [dev]: " ENVIRONMENT
ENVIRONMENT="${ENVIRONMENT:-dev}"

if [[ ! "$ENVIRONMENT" =~ ^(dev|staging|prod)$ ]]; then
    print_error "Invalid environment: $ENVIRONMENT"
    echo "   Valid values: dev, staging, prod"
    exit 1
fi
print_success "Environment: $ENVIRONMENT"

# Set environment-specific variables
case "$ENVIRONMENT" in
    dev)
        ASPNETCORE_ENVIRONMENT="Development"
        LOG_LEVEL="Information"
        ;;
    staging)
        ASPNETCORE_ENVIRONMENT="Staging"
        LOG_LEVEL="Information"
        ;;
    prod)
        ASPNETCORE_ENVIRONMENT="Production"
        LOG_LEVEL="Warning"
        ;;
esac

# ============================================================================
# Suffix Configuration
# ============================================================================
print_header "Infrastructure Configuration"

echo -e "${CYAN}The suffix was set during infrastructure deployment.${NC}"
echo "You can find it by running:"
echo -e "   ${BLUE}az group list --query \"[?starts_with(name, 'rg-xshopai-$ENVIRONMENT')].{Name:name, Suffix:tags.suffix}\" -o table${NC}"
echo ""

read -p "Enter the infrastructure suffix: " SUFFIX

if [ -z "$SUFFIX" ]; then
    print_error "Suffix is required. Please run the infrastructure deployment first."
    exit 1
fi

# Validate suffix format
if [[ ! "$SUFFIX" =~ ^[a-z0-9]{3,6}$ ]]; then
    print_error "Invalid suffix format: $SUFFIX"
    echo "   Suffix must be 3-6 lowercase alphanumeric characters."
    exit 1
fi
print_success "Using suffix: $SUFFIX"

# ============================================================================
# Derive Resource Names from Infrastructure
# ============================================================================
# These names must match what was created by deploy-infra.sh
RESOURCE_GROUP="rg-${PROJECT_NAME}-${ENVIRONMENT}-${SUFFIX}"
ACR_NAME="${PROJECT_NAME}${ENVIRONMENT}${SUFFIX}"
CONTAINER_ENV="cae-${PROJECT_NAME}-${ENVIRONMENT}-${SUFFIX}"
SQL_SERVER="sql-${PROJECT_NAME}-${ENVIRONMENT}-${SUFFIX}"
KEY_VAULT="kv-${PROJECT_NAME}-${ENVIRONMENT}-${SUFFIX}"
MANAGED_IDENTITY="id-${PROJECT_NAME}-${ENVIRONMENT}-${SUFFIX}"

print_info "Derived resource names:"
echo "   Resource Group:      $RESOURCE_GROUP"
echo "   Container Registry:  $ACR_NAME"
echo "   Container Env:       $CONTAINER_ENV"
echo "   SQL Server:          $SQL_SERVER"
echo "   Key Vault:           $KEY_VAULT"
echo ""

# ============================================================================
# Verify Infrastructure Exists
# ============================================================================
print_header "Verifying Infrastructure"

# Check Resource Group
if ! az group show --name "$RESOURCE_GROUP" &> /dev/null; then
    print_error "Resource group '$RESOURCE_GROUP' does not exist."
    echo ""
    echo "Please run the infrastructure deployment first:"
    echo -e "   ${BLUE}cd infrastructure/azure/aca/scripts${NC}"
    echo -e "   ${BLUE}./deploy-infra.sh${NC}"
    exit 1
fi
print_success "Resource Group exists: $RESOURCE_GROUP"

# Check ACR
if ! az acr show --name "$ACR_NAME" &> /dev/null; then
    print_error "Container Registry '$ACR_NAME' does not exist."
    exit 1
fi
ACR_LOGIN_SERVER=$(az acr show --name "$ACR_NAME" --query loginServer -o tsv)
print_success "Container Registry exists: $ACR_LOGIN_SERVER"

# Check Container Apps Environment
if ! az containerapp env show --name "$CONTAINER_ENV" --resource-group "$RESOURCE_GROUP" &> /dev/null; then
    print_error "Container Apps Environment '$CONTAINER_ENV' does not exist."
    exit 1
fi
print_success "Container Apps Environment exists: $CONTAINER_ENV"

# Check SQL Server
SQL_HOST=$(az sql server show --name "$SQL_SERVER" --resource-group "$RESOURCE_GROUP" --query fullyQualifiedDomainName -o tsv 2>/dev/null || echo "")
if [ -z "$SQL_HOST" ]; then
    print_error "SQL Server '$SQL_SERVER' does not exist."
    print_info "Please ensure the infrastructure deployment included SQL Server."
    exit 1
fi
print_success "SQL Server exists: $SQL_HOST"

# Get Managed Identity ID
IDENTITY_ID=$(MSYS_NO_PATHCONV=1 az identity show --name "$MANAGED_IDENTITY" --resource-group "$RESOURCE_GROUP" --query id -o tsv 2>/dev/null || echo "")
if [ -z "$IDENTITY_ID" ]; then
    print_warning "Managed Identity not found, will deploy without it"
else
    print_success "Managed Identity exists: $MANAGED_IDENTITY"
    IDENTITY_CLIENT_ID=$(az identity show --name "$MANAGED_IDENTITY" --resource-group "$RESOURCE_GROUP" --query clientId -o tsv)
fi

# ============================================================================
# Get Secrets from Key Vault
# ============================================================================
print_header "Retrieving Secrets from Key Vault"

# Get JWT secret for authentication
print_info "Retrieving JWT_SECRET from Key Vault..."
JWT_SECRET=$(az keyvault secret show --vault-name "$KEY_VAULT" --name "jwt-secret" --query value -o tsv 2>/dev/null || echo "")
if [ -z "$JWT_SECRET" ]; then
    print_warning "JWT_SECRET not found in Key Vault. JWT validation may fail."
else
    print_success "JWT_SECRET retrieved"
fi

# Get SQL connection string (Azure AD auth)
print_info "Retrieving SQL connection string from Key Vault..."
SQL_CONNECTION=$(az keyvault secret show --vault-name "$KEY_VAULT" --name "sql-connection" --query value -o tsv 2>/dev/null || echo "")
if [ -z "$SQL_CONNECTION" ]; then
    print_warning "SQL connection string not found in Key Vault."
    SQL_CONNECTION="Server=$SQL_HOST;Database=$DATABASE_NAME;Authentication=Active Directory Default;TrustServerCertificate=True;Encrypt=True"
    print_info "Using default connection string: Server=$SQL_HOST;Database=$DATABASE_NAME;Authentication=Active Directory Default"
else
    print_success "SQL connection string retrieved"
fi

# ============================================================================
# Step 1: Create SQL Database
# ============================================================================
print_header "Step 1: Setting up SQL Database"

# Check if database exists
print_info "Checking if database '$DATABASE_NAME' exists..."
DB_EXISTS=$(az sql db show --resource-group "$RESOURCE_GROUP" --server "$SQL_SERVER" --name "$DATABASE_NAME" --query name -o tsv 2>/dev/null || echo "")

if [ -z "$DB_EXISTS" ]; then
    print_info "Creating database '$DATABASE_NAME'..."
    az sql db create \
        --resource-group "$RESOURCE_GROUP" \
        --server "$SQL_SERVER" \
        --name "$DATABASE_NAME" \
        --edition Basic \
        --capacity 5 \
        --max-size 2GB \
        --output none
    print_success "Database created: $DATABASE_NAME"
else
    print_success "Database already exists: $DATABASE_NAME"
fi

# Grant managed identity access to database
if [ -n "$IDENTITY_CLIENT_ID" ]; then
    print_info "Configuring managed identity SQL access..."
    
    # Try to configure automatically using sqlcmd
    if command -v sqlcmd &> /dev/null; then
        ACCESS_TOKEN=$(az account get-access-token --resource https://database.windows.net --query accessToken -o tsv 2>/dev/null || echo "")
        
        if [ -n "$ACCESS_TOKEN" ]; then
            print_info "Granting SQL permissions to managed identity..."
            SQL_SCRIPT="
IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = '$MANAGED_IDENTITY')
BEGIN
    CREATE USER [$MANAGED_IDENTITY] FROM EXTERNAL PROVIDER;
END
ALTER ROLE db_datareader ADD MEMBER [$MANAGED_IDENTITY];
ALTER ROLE db_datawriter ADD MEMBER [$MANAGED_IDENTITY];
ALTER ROLE db_ddladmin ADD MEMBER [$MANAGED_IDENTITY];
PRINT 'Configured: $MANAGED_IDENTITY';
"
            echo "$SQL_SCRIPT" | sqlcmd -S "$SQL_HOST" -d "$DATABASE_NAME" -G -I 2>/dev/null
            if [ $? -eq 0 ]; then
                print_success "SQL permissions granted to managed identity"
            else
                print_warning "Auto-configuration failed. Manual setup may be required."
            fi
        fi
    else
        print_info "Note: sqlcmd not found. Managed identity access may need manual configuration."
        print_info "If the service fails to connect, run these SQL commands in Azure Portal:"
        echo ""
        echo "   CREATE USER [$MANAGED_IDENTITY] FROM EXTERNAL PROVIDER;"
        echo "   ALTER ROLE db_datareader ADD MEMBER [$MANAGED_IDENTITY];"
        echo "   ALTER ROLE db_datawriter ADD MEMBER [$MANAGED_IDENTITY];"
        echo "   ALTER ROLE db_ddladmin ADD MEMBER [$MANAGED_IDENTITY];"
        echo ""
    fi
fi

# ============================================================================
# Confirmation
# ============================================================================
print_header "Deployment Configuration Summary"

echo -e "${CYAN}Environment:${NC}          $ENVIRONMENT"
echo -e "${CYAN}Suffix:${NC}               $SUFFIX"
echo -e "${CYAN}Resource Group:${NC}       $RESOURCE_GROUP"
echo -e "${CYAN}Container Registry:${NC}   $ACR_LOGIN_SERVER"
echo -e "${CYAN}Container Env:${NC}        $CONTAINER_ENV"
echo -e "${CYAN}SQL Server:${NC}           $SQL_HOST"
echo -e "${CYAN}Database:${NC}             $DATABASE_NAME"
echo ""
echo -e "${CYAN}Service Configuration:${NC}"
echo -e "   Service Name:      $SERVICE_NAME"
echo -e "   Service Version:   $SERVICE_VERSION"
echo -e "   App Port:          $APP_PORT"
echo -e "   .NET Environment:  $ASPNETCORE_ENVIRONMENT"
echo -e "   LOG_LEVEL:         $LOG_LEVEL"
echo -e "   Dapr HTTP Port:    $DAPR_HTTP_PORT"
echo -e "   Dapr PubSub:       $DAPR_PUBSUB_NAME"
echo ""

read -p "Do you want to proceed with deployment? (y/N): " CONFIRM
if [[ ! "$CONFIRM" =~ ^[Yy]$ ]]; then
    print_warning "Deployment cancelled by user"
    exit 0
fi

# ============================================================================
# Step 2: Build and Push Container Image
# ============================================================================
print_header "Step 2: Building and Pushing Container Image"

# Login to ACR
print_info "Logging into ACR..."
az acr login --name "$ACR_NAME"
print_success "Logged into ACR"

# Navigate to service directory
cd "$SERVICE_DIR"

# Build Docker image (using production target)
print_info "Building Docker image (this may take a few minutes for .NET)..."
docker build --target production -t "$SERVICE_NAME:latest" .
print_success "Docker image built"

# Tag and push
IMAGE_TAG="$ACR_LOGIN_SERVER/$SERVICE_NAME:latest"
docker tag "$SERVICE_NAME:latest" "$IMAGE_TAG"
print_info "Pushing image to ACR..."
docker push "$IMAGE_TAG"
print_success "Image pushed: $IMAGE_TAG"

# ============================================================================
# Step 3: Deploy Container App
# ============================================================================
print_header "Step 3: Deploying Container App"

# Get ACR credentials
ACR_PASSWORD=$(az acr credential show --name "$ACR_NAME" --query "passwords[0].value" -o tsv)

# Build environment variables
ENV_VARS=("ASPNETCORE_ENVIRONMENT=$ASPNETCORE_ENVIRONMENT")
ENV_VARS+=("ASPNETCORE_URLS=http://+:$APP_PORT")
ENV_VARS+=("Logging__LogLevel__Default=$LOG_LEVEL")
ENV_VARS+=("Dapr__Enabled=true")
ENV_VARS+=("Dapr__HttpPort=$DAPR_HTTP_PORT")
ENV_VARS+=("Dapr__PubSubName=$DAPR_PUBSUB_NAME")
ENV_VARS+=("Dapr__AppId=$SERVICE_NAME")

# Add Azure Client ID for managed identity (required for DefaultAzureCredential)
if [ -n "$IDENTITY_CLIENT_ID" ]; then
    ENV_VARS+=("AZURE_CLIENT_ID=$IDENTITY_CLIENT_ID")
fi

# Add database connection string (fallback format for DaprSecretService)
# The code converts database:connectionString → database_connectionString for fallback
ENV_VARS+=("database_connectionString=$SQL_CONNECTION")

# Add JWT secrets if available (fallback format for DaprSecretService)
# The code converts jwt:secret → jwt_secret, jwt:issuer → jwt_issuer, etc.
if [ -n "$JWT_SECRET" ]; then
    ENV_VARS+=("jwt_secret=$JWT_SECRET")
    ENV_VARS+=("jwt_issuer=auth-service")
    ENV_VARS+=("jwt_audience=xshopai-platform")
fi

# Check if container app exists
if az containerapp show --name "$SERVICE_NAME" --resource-group "$RESOURCE_GROUP" &> /dev/null; then
    print_info "Container app '$SERVICE_NAME' exists, updating..."
    az containerapp update \
        --name "$SERVICE_NAME" \
        --resource-group "$RESOURCE_GROUP" \
        --image "$IMAGE_TAG" \
        --set-env-vars "${ENV_VARS[@]}" \
        --output none
    print_success "Container app updated"
else
    print_info "Creating container app '$SERVICE_NAME'..."
    
    # Build the create command
    MSYS_NO_PATHCONV=1 az containerapp create \
        --name "$SERVICE_NAME" \
        --resource-group "$RESOURCE_GROUP" \
        --environment "$CONTAINER_ENV" \
        --image "$IMAGE_TAG" \
        --registry-server "$ACR_LOGIN_SERVER" \
        --registry-username "$ACR_NAME" \
        --registry-password "$ACR_PASSWORD" \
        --target-port $APP_PORT \
        --ingress external \
        --min-replicas 1 \
        --max-replicas 5 \
        --cpu 0.5 \
        --memory 1.0Gi \
        --enable-dapr \
        --dapr-app-id "$SERVICE_NAME" \
        --dapr-app-port $APP_PORT \
        --env-vars "${ENV_VARS[@]}" \
        ${IDENTITY_ID:+--user-assigned "$IDENTITY_ID"} \
        --output none
    
    print_success "Container app created"
fi

# ============================================================================
# Step 4: Verify Deployment
# ============================================================================
print_header "Step 4: Verifying Deployment"

# Get app FQDN (internal ingress)
APP_FQDN=$(az containerapp show \
    --name "$SERVICE_NAME" \
    --resource-group "$RESOURCE_GROUP" \
    --query properties.configuration.ingress.fqdn \
    -o tsv)

print_success "Deployment completed!"
echo ""
print_info "Service FQDN: $APP_FQDN"
echo ""

# Check container app status
sleep 10
APP_STATUS=$(az containerapp show \
    --name "$SERVICE_NAME" \
    --resource-group "$RESOURCE_GROUP" \
    --query properties.runningStatus \
    -o tsv 2>/dev/null || echo "Unknown")

if [ "$APP_STATUS" = "Running" ]; then
    print_success "Container app is running!"
else
    print_warning "Container app status: $APP_STATUS. The app may still be starting."
fi

# ============================================================================
# Summary
# ============================================================================
print_header "Deployment Summary"

echo -e "${GREEN}==============================================================================${NC}"
echo -e "${GREEN}   ✅ $SERVICE_NAME DEPLOYED SUCCESSFULLY${NC}"
echo -e "${GREEN}==============================================================================${NC}"
echo ""
echo -e "${CYAN}Application:${NC}"
echo "   FQDN:             $APP_FQDN"
echo "   Ingress:          external"
echo "   Health:           /health"
echo "   Readiness:        /health/ready"
echo "   Liveness:         /health/live"
echo "   Metrics:          /metrics"
echo ""
echo -e "${CYAN}Database:${NC}"
echo "   SQL Server:       $SQL_HOST"
echo "   Database:         $DATABASE_NAME"
echo "   Authentication:   Azure AD Default (Managed Identity)"
echo ""
echo -e "${CYAN}Infrastructure:${NC}"
echo "   Resource Group:   $RESOURCE_GROUP"
echo "   Environment:      $CONTAINER_ENV"
echo "   Registry:         $ACR_LOGIN_SERVER"
echo ""
echo -e "${CYAN}Dapr Configuration:${NC}"
echo "   App ID:           $SERVICE_NAME"
echo "   HTTP Port:        $DAPR_HTTP_PORT"
echo "   gRPC Port:        $DAPR_GRPC_PORT"
echo "   PubSub:           $DAPR_PUBSUB_NAME"
echo ""
echo -e "${CYAN}Dapr Service Invocation (from other services):${NC}"
echo "   http://localhost:\$DAPR_HTTP_PORT/v1.0/invoke/$SERVICE_NAME/method/{endpoint}"
echo ""
echo -e "${CYAN}Useful Commands:${NC}"
echo -e "   View logs:        ${BLUE}az containerapp logs show --name $SERVICE_NAME --resource-group $RESOURCE_GROUP --follow${NC}"
echo -e "   View Dapr logs:   ${BLUE}az containerapp logs show --name $SERVICE_NAME --resource-group $RESOURCE_GROUP --container daprd --follow${NC}"
echo -e "   Delete app:       ${BLUE}az containerapp delete --name $SERVICE_NAME --resource-group $RESOURCE_GROUP --yes${NC}"
echo ""
echo -e "${YELLOW}Note: If using Azure AD authentication for SQL, ensure the managed identity${NC}"
echo -e "${YELLOW}has been granted database permissions (see Step 1 output for SQL commands).${NC}"
echo ""
