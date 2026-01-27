#!/usr/bin/env pwsh
# Order Service - PowerShell Run Script with Dapr
# Port: 8006, Dapr HTTP: 3506, Dapr gRPC: 50006

Write-Host ""
Write-Host "============================================" -ForegroundColor Cyan
#!/usr/bin/env pwsh
# Run Order Service with Dapr sidecar
# Usage: .\run.ps1

$Host.UI.RawUI.WindowTitle = "Order Service"

Write-Host "Starting Order Service with Dapr..." -ForegroundColor Green
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""

# Kill any existing processes on ports
Write-Host "Cleaning up existing processes..." -ForegroundColor Yellow

# Kill process on port 8006 (app port)
$process = Get-NetTCPConnection -LocalPort 8006 -ErrorAction SilentlyContinue | Select-Object -ExpandProperty OwningProcess -Unique
if ($process) {
    Write-Host "Killing process on port 8006 (PID: $process)" -ForegroundColor Yellow
    Stop-Process -Id $process -Force -ErrorAction SilentlyContinue
}

# Kill process on port 3506 (Dapr HTTP port)
$process = Get-NetTCPConnection -LocalPort 3506 -ErrorAction SilentlyContinue | Select-Object -ExpandProperty OwningProcess -Unique
if ($process) {
    Write-Host "Killing process on port 3506 (PID: $process)" -ForegroundColor Yellow
    Stop-Process -Id $process -Force -ErrorAction SilentlyContinue
}

# Kill process on port 50006 (Dapr gRPC port)
$process = Get-NetTCPConnection -LocalPort 50006 -ErrorAction SilentlyContinue | Select-Object -ExpandProperty OwningProcess -Unique
if ($process) {
    Write-Host "Killing process on port 50006 (PID: $process)" -ForegroundColor Yellow
    Stop-Process -Id $process -Force -ErrorAction SilentlyContinue
}

Start-Sleep -Seconds 2

Write-Host ""
Write-Host "Starting with Dapr sidecar..." -ForegroundColor Green
Write-Host "App ID: order-service" -ForegroundColor Cyan
Write-Host "App Port: 8006" -ForegroundColor Cyan
Write-Host "Dapr HTTP Port: 3506" -ForegroundColor Cyan
Write-Host "Dapr gRPC Port: 50006" -ForegroundColor Cyan
Write-Host ""

# Get the directory where this script is located
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

dapr run `
  --app-id order-service `
  --app-port 8006 `
  --dapr-http-port 3506 `
  --dapr-grpc-port 50006 `
  --log-level error `
  --resources-path "$scriptDir/.dapr/components" `
  --config "$scriptDir/.dapr/config.yaml" `
  -- dotnet watch --project "$scriptDir/OrderService.Api/OrderService.Api.csproj" --urls "http://localhost:8006"

Write-Host ""
Write-Host "============================================" -ForegroundColor Cyan
Write-Host "Service stopped." -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
