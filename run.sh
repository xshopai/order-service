#!/bin/bash
# Order Service - Bash Run Script with Dapr
# Port: 8006, Dapr HTTP: 3500, Dapr gRPC: 50001

echo ""
echo "============================================"
echo "Starting order-service with Dapr..."
echo "============================================"
echo ""

# Kill any existing processes on ports
echo "Cleaning up existing processes..."

# Kill processes on port 8006 (app port)
lsof -ti:8006 | xargs kill -9 2>/dev/null || true

# Kill processes on port 3500 (Dapr HTTP port)
lsof -ti:3500 | xargs kill -9 2>/dev/null || true

# Kill processes on port 50001 (Dapr gRPC port)
lsof -ti:50001 | xargs kill -9 2>/dev/null || true

sleep 2

echo ""
echo "Starting with Dapr sidecar..."
echo "App ID: order-service"
echo "App Port: 8006"
echo "Dapr HTTP Port: 3500"
echo "Dapr gRPC Port: 50001"
echo ""

dapr run \
  --app-id order-service \
  --app-port 8006 \
  --dapr-http-port 3500 \
  --dapr-grpc-port 50001 \
  --log-level error \
  --resources-path ./.dapr/components \
  --config ./.dapr/config.yaml \
  -- dotnet run --project OrderService.Api/OrderService.Api.csproj --urls "http://localhost:8006"

echo ""
echo "============================================"
echo "Service stopped."
echo "============================================"
