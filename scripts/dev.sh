#!/bin/bash

# Order Service - Run with direct RabbitMQ (local development)

echo "Starting Order Service (Direct RabbitMQ)..."
echo "Service will be available at: http://localhost:8006"
echo ""

# Kill any process using port 8006 (prevents "address already in use" errors)
PORT=8006
for pid in $(netstat -ano 2>/dev/null | grep ":$PORT" | grep LISTENING | awk '{print $5}' | sort -u); do
    echo "Killing process $pid on port $PORT..."
    taskkill //F //PID $pid 2>/dev/null
done

# Navigate to service root directory
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SERVICE_DIR="$(dirname "$SCRIPT_DIR")"
cd "$SERVICE_DIR"

# Copy appsettings.Development.json to appsettings.json for local development
if [ -f "OrderService.Api/appsettings.Development.json" ]; then
    cp "OrderService.Api/appsettings.Development.json" "OrderService.Api/appsettings.json"
    echo "✅ Copied appsettings.Development.json → appsettings.json"
fi

# Run with .NET
export ASPNETCORE_URLS=http://+:8006
dotnet run --project OrderService.Api/OrderService.Api.csproj --no-launch-profile
