# ============================================================================
# Azure Container Apps Deployment Script for Order Service (PowerShell)
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
#   - Managed Identity for Azure AD authentication
#
# This script will:
#   1. Build and push the Docker image
#   2. Create the SQL database if it doesn't exist
#   3. Configure managed identity SQL permissions
#   4. Deploy the container app with Dapr sidecar
# ============================================================================

$ErrorActionPreference = "Stop"

# -----------------------------------------------------------------------------
# Print Functions
# -----------------------------------------------------------------------------
function Write-Header { 
    param([string]$Message)
    Write-Host "`n==============================================================================" -ForegroundColor Blue
    Write-Host $Message -ForegroundColor Blue
    Write-Host "==============================================================================`n" -ForegroundColor Blue
}

function Write-Success { param([string]$Message); Write-Host "✓ $Message" -ForegroundColor Green }
function Write-Warning { param([string]$Message); Write-Host "⚠ $Message" -ForegroundColor Yellow }
function Write-Info { param([string]$Message); Write-Host "ℹ $Message" -ForegroundColor Cyan }
function Write-Error { param([string]$Message); Write-Host "✗ $Message" -ForegroundColor Red }

function Read-HostWithDefault { 
    param([string]$Prompt, [string]$Default)
    $input = Read-Host "$Prompt [$Default]"
    if ([string]::IsNullOrWhiteSpace($input)) { return $Default }
    return $input
}

# ============================================================================
# Service Configuration
# ============================================================================
$ServiceName = "order-service"
$ServiceVersion = "1.0.0"
$AppPort = 8006
$ProjectName = "xshopai"
$DatabaseName = "order_service_db"

# Dapr configuration (per PORT_CONFIGURATION.md: order-service = 3506/50006)
$DaprHttpPort = 3506
$DaprGrpcPort = 50006
$DaprPubSubName = "pubsub"

# Get script directory and service directory
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ServiceDir = Split-Path -Parent $ScriptDir

# ============================================================================
# Prerequisites Check
# ============================================================================
Write-Header "Checking Prerequisites"

# Check Azure CLI
try { 
    az version | Out-Null
    Write-Success "Azure CLI installed" 
} catch { 
    Write-Error "Azure CLI not installed. Please install from: https://docs.microsoft.com/en-us/cli/azure/install-azure-cli"
    exit 1 
}

# Check Docker
try { 
    docker version | Out-Null
    Write-Success "Docker installed" 
} catch { 
    Write-Error "Docker not installed. Please install Docker first."
    exit 1 
}

# Check .NET SDK
try { 
    dotnet --version | Out-Null
    Write-Success ".NET SDK installed" 
} catch { 
    Write-Warning ".NET SDK not installed. Database migrations will be skipped."
}

# Check Azure login
try { 
    az account show | Out-Null 
} catch { 
    Write-Warning "Not logged into Azure. Initiating login..."
    az login 
}
Write-Success "Logged into Azure"

# ============================================================================
# Environment Selection
# ============================================================================
Write-Header "Environment Selection"

Write-Host "Available Environments:" -ForegroundColor Cyan
Write-Host "   dev     - Development environment"
Write-Host "   staging - Staging/QA environment"
Write-Host "   prod    - Production environment"
Write-Host ""

$Environment = Read-HostWithDefault -Prompt "Enter environment (dev/staging/prod)" -Default "dev"

if ($Environment -notmatch '^(dev|staging|prod)$') {
    Write-Error "Invalid environment: $Environment"
    Write-Host "   Valid values: dev, staging, prod"
    exit 1
}
Write-Success "Environment: $Environment"

# Set environment-specific variables
switch ($Environment) {
    "dev" {
        $AspNetCoreEnvironment = "Development"
        $LogLevel = "Information"
    }
    "staging" {
        $AspNetCoreEnvironment = "Staging"
        $LogLevel = "Information"
    }
    "prod" {
        $AspNetCoreEnvironment = "Production"
        $LogLevel = "Warning"
    }
}

# ============================================================================
# Infrastructure Suffix Configuration
# ============================================================================
Write-Header "Infrastructure Configuration"

Write-Host "The suffix was set during infrastructure deployment." -ForegroundColor Cyan
Write-Host "You can find it by running:"
Write-Host "   az group list --query `"[?starts_with(name, 'rg-xshopai-$Environment')].{Name:name, Suffix:tags.suffix}`" -o table" -ForegroundColor Blue
Write-Host ""

$Suffix = Read-Host "Enter the infrastructure suffix"

if ([string]::IsNullOrWhiteSpace($Suffix)) {
    Write-Error "Suffix is required. Please run the infrastructure deployment first."
    exit 1
}

# Validate suffix format
if ($Suffix -notmatch '^[a-z0-9]{3,6}$') {
    Write-Error "Invalid suffix format: $Suffix"
    Write-Host "   Suffix must be 3-6 lowercase alphanumeric characters."
    exit 1
}
Write-Success "Using suffix: $Suffix"

# ============================================================================
# Derive Resource Names from Infrastructure
# ============================================================================
$ResourceGroup = "rg-$ProjectName-$Environment-$Suffix"
$AcrName = "$ProjectName$Environment$Suffix"
$ContainerEnv = "cae-$ProjectName-$Environment-$Suffix"
$SqlServer = "sql-$ProjectName-$Environment-$Suffix"
$KeyVault = "kv-$ProjectName-$Environment-$Suffix"
$ManagedIdentity = "id-$ProjectName-$Environment-$Suffix"

Write-Info "Derived resource names:"
Write-Host "   Resource Group:      $ResourceGroup"
Write-Host "   Container Registry:  $AcrName"
Write-Host "   Container Env:       $ContainerEnv"
Write-Host "   SQL Server:          $SqlServer"
Write-Host "   Key Vault:           $KeyVault"
Write-Host "   Managed Identity:    $ManagedIdentity"
Write-Host ""

# ============================================================================
# Verify Infrastructure Exists
# ============================================================================
Write-Header "Verifying Infrastructure"

# Check Resource Group
$rgExists = az group show --name $ResourceGroup 2>$null
if (-not $rgExists) {
    Write-Error "Resource group '$ResourceGroup' does not exist."
    Write-Host ""
    Write-Host "Please run the infrastructure deployment first:"
    Write-Host "   cd infrastructure/azure/aca/scripts" -ForegroundColor Blue
    Write-Host "   ./deploy-infra.sh" -ForegroundColor Blue
    exit 1
}
Write-Success "Resource Group exists: $ResourceGroup"

# Check ACR
$acrExists = az acr show --name $AcrName 2>$null
if (-not $acrExists) {
    Write-Error "Container Registry '$AcrName' does not exist."
    exit 1
}
$AcrLoginServer = az acr show --name $AcrName --query loginServer -o tsv
Write-Success "Container Registry exists: $AcrLoginServer"

# Check Container Apps Environment
$envExists = az containerapp env show --name $ContainerEnv --resource-group $ResourceGroup 2>$null
if (-not $envExists) {
    Write-Error "Container Apps Environment '$ContainerEnv' does not exist."
    exit 1
}
Write-Success "Container Apps Environment exists: $ContainerEnv"

# Check SQL Server
$SqlHost = az sql server show --name $SqlServer --resource-group $ResourceGroup --query fullyQualifiedDomainName -o tsv 2>$null
if ([string]::IsNullOrWhiteSpace($SqlHost)) {
    Write-Error "SQL Server '$SqlServer' does not exist."
    Write-Info "Please ensure the infrastructure deployment included SQL Server."
    exit 1
}
Write-Success "SQL Server exists: $SqlHost"

# Get Managed Identity
$IdentityId = az identity show --name $ManagedIdentity --resource-group $ResourceGroup --query id -o tsv 2>$null
if ([string]::IsNullOrWhiteSpace($IdentityId)) {
    Write-Warning "Managed Identity not found, will deploy without it"
    $IdentityClientId = $null
} else {
    Write-Success "Managed Identity exists: $ManagedIdentity"
    $IdentityClientId = az identity show --name $ManagedIdentity --resource-group $ResourceGroup --query clientId -o tsv
}

# ============================================================================
# Get Secrets from Key Vault
# ============================================================================
Write-Header "Retrieving Secrets from Key Vault"

# Get JWT secret for authentication
Write-Info "Retrieving JWT_SECRET from Key Vault..."
$JwtSecret = az keyvault secret show --vault-name $KeyVault --name "jwt-secret" --query value -o tsv 2>$null
if ([string]::IsNullOrWhiteSpace($JwtSecret)) {
    Write-Warning "JWT_SECRET not found in Key Vault. JWT validation may fail."
} else {
    Write-Success "JWT_SECRET retrieved"
}

# Build SQL connection string (Azure AD auth)
$SqlConnection = "Server=$SqlHost;Database=$DatabaseName;Authentication=Active Directory Default;TrustServerCertificate=True;Encrypt=True"
Write-Info "Using Azure AD authentication for SQL: Server=$SqlHost;Database=$DatabaseName"

# ============================================================================
# Step 1: Create SQL Database
# ============================================================================
Write-Header "Step 1: Setting up SQL Database"

# Check if database exists
Write-Info "Checking if database '$DatabaseName' exists..."
$DbExists = az sql db show --resource-group $ResourceGroup --server $SqlServer --name $DatabaseName --query name -o tsv 2>$null

if ([string]::IsNullOrWhiteSpace($DbExists)) {
    Write-Info "Creating database '$DatabaseName'..."
    az sql db create `
        --resource-group $ResourceGroup `
        --server $SqlServer `
        --name $DatabaseName `
        --edition Basic `
        --capacity 5 `
        --max-size 2GB `
        --output none
    Write-Success "Database created: $DatabaseName"
} else {
    Write-Success "Database already exists: $DatabaseName"
}

# Grant managed identity access to database
if (-not [string]::IsNullOrWhiteSpace($IdentityClientId)) {
    Write-Info "Configuring managed identity SQL access..."
    
    try {
        # Get access token for SQL
        $AccessToken = az account get-access-token --resource https://database.windows.net --query accessToken -o tsv
        
        if (-not [string]::IsNullOrWhiteSpace($AccessToken)) {
            Write-Info "Granting SQL permissions to managed identity..."
            
            $SqlScript = @"
IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = '$ManagedIdentity')
BEGIN
    CREATE USER [$ManagedIdentity] FROM EXTERNAL PROVIDER;
END
ALTER ROLE db_datareader ADD MEMBER [$ManagedIdentity];
ALTER ROLE db_datawriter ADD MEMBER [$ManagedIdentity];
ALTER ROLE db_ddladmin ADD MEMBER [$ManagedIdentity];
PRINT 'Configured: $ManagedIdentity';
"@
            
            # Use Invoke-Sqlcmd with access token
            Invoke-Sqlcmd -ServerInstance $SqlHost -Database $DatabaseName -AccessToken $AccessToken -Query $SqlScript -ErrorAction SilentlyContinue
            Write-Success "SQL permissions granted to managed identity"
        }
    } catch {
        Write-Warning "Auto-configuration failed: $($_.Exception.Message)"
        Write-Info "Manual setup may be required. Run these SQL commands in Azure Portal:"
        Write-Host ""
        Write-Host "   CREATE USER [$ManagedIdentity] FROM EXTERNAL PROVIDER;"
        Write-Host "   ALTER ROLE db_datareader ADD MEMBER [$ManagedIdentity];"
        Write-Host "   ALTER ROLE db_datawriter ADD MEMBER [$ManagedIdentity];"
        Write-Host "   ALTER ROLE db_ddladmin ADD MEMBER [$ManagedIdentity];"
        Write-Host ""
    }
}

# ============================================================================
# Confirmation
# ============================================================================
Write-Header "Deployment Configuration Summary"

Write-Host "Environment:          $Environment" -ForegroundColor Cyan
Write-Host "Suffix:               $Suffix" -ForegroundColor Cyan
Write-Host "Resource Group:       $ResourceGroup" -ForegroundColor Cyan
Write-Host "Container Registry:   $AcrLoginServer" -ForegroundColor Cyan
Write-Host "Container Env:        $ContainerEnv" -ForegroundColor Cyan
Write-Host "SQL Server:           $SqlHost" -ForegroundColor Cyan
Write-Host "Database:             $DatabaseName" -ForegroundColor Cyan
Write-Host "Authentication:       Azure AD Default (Managed Identity)" -ForegroundColor Cyan
Write-Host ""
Write-Host "Service Configuration:" -ForegroundColor Cyan
Write-Host "   Service Name:      $ServiceName"
Write-Host "   Service Version:   $ServiceVersion"
Write-Host "   App Port:          $AppPort"
Write-Host "   .NET Environment:  $AspNetCoreEnvironment"
Write-Host "   LOG_LEVEL:         $LogLevel"
Write-Host "   Dapr HTTP Port:    $DaprHttpPort"
Write-Host "   Dapr PubSub:       $DaprPubSubName"
Write-Host ""

$Confirm = Read-Host "Do you want to proceed with deployment? (y/N)"
if ($Confirm -notmatch '^[Yy]$') {
    Write-Warning "Deployment cancelled by user"
    exit 0
}

# ============================================================================
# Step 2: Build and Push Container Image
# ============================================================================
Write-Header "Step 2: Building and Pushing Container Image"

# Login to ACR
Write-Info "Logging into ACR..."
az acr login --name $AcrName
Write-Success "Logged into ACR"

# Navigate to service directory
Push-Location $ServiceDir

try {
    # Build Docker image (using production target)
    Write-Info "Building Docker image (this may take a few minutes for .NET)..."
    docker build --target production -t "${ServiceName}:latest" .
    Write-Success "Docker image built"

    # Tag and push
    $ImageTag = "$AcrLoginServer/${ServiceName}:latest"
    docker tag "${ServiceName}:latest" $ImageTag
    Write-Info "Pushing image to ACR..."
    docker push $ImageTag
    Write-Success "Image pushed: $ImageTag"
} finally {
    Pop-Location
}

# ============================================================================
# Step 3: Deploy Container App
# ============================================================================
Write-Header "Step 3: Deploying Container App"

# Get ACR credentials
$AcrPassword = az acr credential show --name $AcrName --query "passwords[0].value" -o tsv

# Build environment variables array
$EnvVars = @(
    "ASPNETCORE_ENVIRONMENT=$AspNetCoreEnvironment",
    "ASPNETCORE_URLS=http://+:$AppPort",
    "Logging__LogLevel__Default=$LogLevel",
    "Dapr__Enabled=true",
    "Dapr__HttpPort=$DaprHttpPort",
    "Dapr__PubSubName=$DaprPubSubName",
    "Dapr__AppId=$ServiceName"
)

# Add Azure Client ID for managed identity (required for DefaultAzureCredential)
if (-not [string]::IsNullOrWhiteSpace($IdentityClientId)) {
    $EnvVars += "AZURE_CLIENT_ID=$IdentityClientId"
}

# Add database connection string (fallback format for DaprSecretService)
$EnvVars += "database_connectionString=$SqlConnection"

# Add JWT secrets if available (fallback format for DaprSecretService)
if (-not [string]::IsNullOrWhiteSpace($JwtSecret)) {
    $EnvVars += "jwt_secret=$JwtSecret"
    $EnvVars += "jwt_issuer=auth-service"
    $EnvVars += "jwt_audience=xshopai-platform"
}

# Check if container app exists
$appExists = az containerapp show --name $ServiceName --resource-group $ResourceGroup 2>$null
if ($appExists) {
    Write-Info "Container app '$ServiceName' exists, updating..."
    $envVarsString = $EnvVars -join " "
    az containerapp update `
        --name $ServiceName `
        --resource-group $ResourceGroup `
        --image $ImageTag `
        --set-env-vars $EnvVars `
        --output none
    Write-Success "Container app updated"
} else {
    Write-Info "Creating container app '$ServiceName'..."
    
    $createArgs = @(
        "--name", $ServiceName,
        "--resource-group", $ResourceGroup,
        "--environment", $ContainerEnv,
        "--image", $ImageTag,
        "--registry-server", $AcrLoginServer,
        "--registry-username", $AcrName,
        "--registry-password", $AcrPassword,
        "--target-port", $AppPort,
        "--ingress", "external",
        "--min-replicas", "1",
        "--max-replicas", "5",
        "--cpu", "0.5",
        "--memory", "1.0Gi",
        "--enable-dapr",
        "--dapr-app-id", $ServiceName,
        "--dapr-app-port", $AppPort,
        "--env-vars"
    ) + $EnvVars
    
    if (-not [string]::IsNullOrWhiteSpace($IdentityId)) {
        $createArgs += @("--user-assigned", $IdentityId)
    }
    
    $createArgs += "--output", "none"
    
    az containerapp create @createArgs
    Write-Success "Container app created"
}

# ============================================================================
# Step 4: Verify Deployment
# ============================================================================
Write-Header "Step 4: Verifying Deployment"

# Get app FQDN
$AppFqdn = az containerapp show `
    --name $ServiceName `
    --resource-group $ResourceGroup `
    --query properties.configuration.ingress.fqdn `
    -o tsv

Write-Success "Deployment completed!"
Write-Host ""
Write-Info "Service FQDN: https://$AppFqdn"
Write-Host ""

# Check container app status
Start-Sleep -Seconds 10
$AppStatus = az containerapp show `
    --name $ServiceName `
    --resource-group $ResourceGroup `
    --query properties.runningStatus `
    -o tsv 2>$null

if ($AppStatus -eq "Running") {
    Write-Success "Container app is running!"
} else {
    Write-Warning "Container app status: $AppStatus. The app may still be starting."
}

# ============================================================================
# Summary
# ============================================================================
Write-Header "Deployment Summary"

Write-Host "Service Details:" -ForegroundColor Cyan
Write-Host "   Service Name:     $ServiceName"
Write-Host "   Environment:      $Environment"
Write-Host "   FQDN:             https://$AppFqdn"
Write-Host "   Dapr App ID:      $ServiceName"
Write-Host "   Database:         $DatabaseName"
Write-Host "   Authentication:   Azure AD Default (Managed Identity)"
Write-Host ""
Write-Host "Health Check Endpoints:" -ForegroundColor Cyan
Write-Host "   Health:           https://$AppFqdn/health"
Write-Host "   Readiness:        https://$AppFqdn/health/ready"
Write-Host "   Liveness:         https://$AppFqdn/health/live"
Write-Host "   Metrics:          https://$AppFqdn/metrics"
Write-Host ""

Write-Host "Useful Commands:" -ForegroundColor Cyan
Write-Host "   View logs:        az containerapp logs show -n $ServiceName -g $ResourceGroup --type console --tail 100"
Write-Host "   Restart:          az containerapp revision restart -n $ServiceName -g $ResourceGroup --revision <revision>"
Write-Host "   Scale:            az containerapp update -n $ServiceName -g $ResourceGroup --min-replicas 2"
Write-Host ""

if (-not [string]::IsNullOrWhiteSpace($IdentityClientId)) {
    Write-Host "Note: Using Azure AD authentication for SQL with managed identity" -ForegroundColor Yellow
    Write-Host "      AZURE_CLIENT_ID=$IdentityClientId" -ForegroundColor Yellow
    Write-Host "      The app uses DefaultAzureCredential to authenticate to SQL Server." -ForegroundColor Yellow
}
