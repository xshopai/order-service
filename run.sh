#!/bin/bash
# Order Service - Bash Run Script with Dapr
# Port: 8006, Dapr HTTP: 3506, Dapr gRPC: 50006

echo ""
echo "============================================"
echo "Starting order-service with Dapr..."
echo "============================================"
echo ""

# Kill any existing processes on ports
echo "Cleaning up existing processes..."

# Kill processes on port 8006 (app port)
lsof -ti:8006 | xargs kill -9 2>/dev/null || true

# Kill processes on port 3506 (Dapr HTTP port)
lsof -ti:3506 | xargs kill -9 2>/dev/null || true

# Kill processes on port 50006 (Dapr gRPC port)
lsof -ti:50006 | xargs kill -9 2>/dev/null || true

sleep 2

echo ""
echo "Starting with Dapr sidecar..."
echo "App ID: order-service"
echo "App Port: 8006"
echo "Dapr HTTP Port: 3506"
echo "Dapr gRPC Port: 50006"
echo ""

dapr run \
  --app-id order-service \
  --app-port 8006 \
  --dapr-http-port 3506 \
  --dapr-grpc-port 50006 \
  --log-level error \
  --resources-path ./.dapr/components \
  --config ./.dapr/config.yaml \
  -- dotnet run --project OrderService.Api/OrderService.Api.csproj --urls "http://localhost:8006"

echo ""
echo "============================================"
echo "Service stopped."
echo "============================================"
