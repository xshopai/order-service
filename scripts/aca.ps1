# ============================================================================
# Azure Container Apps Deployment Script for Order Service (PowerShell)
# ============================================================================

$ErrorActionPreference = "Stop"

function Write-Header { param([string]$Message); Write-Host "`n============================================================================" -ForegroundColor Blue; Write-Host $Message -ForegroundColor Blue; Write-Host "============================================================================`n" -ForegroundColor Blue }
function Write-Success { param([string]$Message); Write-Host "✓ $Message" -ForegroundColor Green }
function Write-Info { param([string]$Message); Write-Host "ℹ $Message" -ForegroundColor Blue }

function Read-HostWithDefault { param([string]$Prompt, [string]$Default); $input = Read-Host "$Prompt [$Default]"; if ([string]::IsNullOrWhiteSpace($input)) { return $Default }; return $input }

Write-Header "Checking Prerequisites"
try { az version | Out-Null; Write-Success "Azure CLI installed" } catch { Write-Error "Azure CLI not installed"; exit 1 }
try { docker version | Out-Null; Write-Success "Docker installed" } catch { Write-Error "Docker not installed"; exit 1 }
try { dotnet --version | Out-Null; Write-Success ".NET SDK installed" } catch { Write-Error ".NET SDK not installed"; exit 1 }
try { az account show | Out-Null } catch { az login }

Write-Header "Azure Configuration"
$ResourceGroup = Read-HostWithDefault -Prompt "Enter Resource Group name" -Default "rg-xshopai-aca"
$Location = Read-HostWithDefault -Prompt "Enter Azure Location" -Default "swedencentral"
$AcrName = Read-HostWithDefault -Prompt "Enter Azure Container Registry name" -Default "acrxshopaiaca"
$EnvironmentName = Read-HostWithDefault -Prompt "Enter Container Apps Environment name" -Default "cae-xshopai-aca"
$PostgresServer = Read-HostWithDefault -Prompt "Enter PostgreSQL server name" -Default "psql-xshopai-aca"
$PostgresPassword = Read-Host "Enter PostgreSQL admin password" -AsSecureString

$AppName = "order-service"
$AppPort = 1006
$DatabaseName = "orders_db"
$DbPassword = [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($PostgresPassword))

$Confirm = Read-Host "Proceed with deployment? (y/N)"
if ($Confirm -notmatch '^[Yy]$') { exit 0 }

Write-Header "Setting Up PostgreSQL Database"
try {
    az postgres flexible-server show --name $PostgresServer --resource-group $ResourceGroup | Out-Null
    Write-Info "PostgreSQL server exists"
} catch {
    az postgres flexible-server create --name $PostgresServer --resource-group $ResourceGroup --location $Location --admin-user pgadmin --admin-password $DbPassword --sku-name Standard_B1ms --tier Burstable --storage-size 32 --output none
    Write-Success "PostgreSQL server created"
}

$PostgresHost = "${PostgresServer}.postgres.database.azure.com"
$ConnectionString = "Host=${PostgresHost};Database=${DatabaseName};Username=pgadmin;Password=${DbPassword};SSL Mode=Require"

Write-Header "Building and Deploying"
$AcrLoginServer = az acr show --name $AcrName --query loginServer -o tsv
az acr login --name $AcrName

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ServiceDir = Split-Path -Parent $ScriptDir
Push-Location $ServiceDir

try {
    dotnet publish OrderService.Api/OrderService.Api.csproj -c Release -o ./publish
    $ImageTag = "${AcrLoginServer}/${AppName}:latest"
    docker build -t $ImageTag .
    docker push $ImageTag
    Write-Success "Image pushed"
} finally { Pop-Location }

az containerapp env show --name $EnvironmentName --resource-group $ResourceGroup | Out-Null 2>$null
if ($LASTEXITCODE -ne 0) {
    az containerapp env create --name $EnvironmentName --resource-group $ResourceGroup --location $Location --output none
}

try {
    az containerapp show --name $AppName --resource-group $ResourceGroup | Out-Null
    az containerapp update --name $AppName --resource-group $ResourceGroup --image $ImageTag --output none
    Write-Success "Container app updated"
} catch {
    az containerapp create `
        --name $AppName `
        --resource-group $ResourceGroup `
        --environment $EnvironmentName `
        --image $ImageTag `
        --registry-server $AcrLoginServer `
        --target-port $AppPort `
        --ingress internal `
        --min-replicas 1 `
        --max-replicas 5 `
        --cpu 0.5 `
        --memory 1Gi `
        --enable-dapr `
        --dapr-app-id $AppName `
        --dapr-app-port $AppPort `
        --secrets "db-conn=$ConnectionString" `
        --env-vars `
            "ASPNETCORE_URLS=http://+:$AppPort" `
            "ASPNETCORE_ENVIRONMENT=Production" `
            "ConnectionStrings__DefaultConnection=secretref:db-conn" `
        --output none
    Write-Success "Container app created"
}

Write-Header "Deployment Complete!"
Write-Host "Order Service deployed! Dapr App ID: $AppName" -ForegroundColor Green
