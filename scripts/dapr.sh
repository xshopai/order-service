#!/bin/bash

# Order Service - Run with Dapr

echo "Starting Order Service with Dapr..."
echo "Service will be available at: http://localhost:8006"
echo "Dapr HTTP endpoint: http://localhost:3506"
echo "Dapr gRPC endpoint: localhost:50006"
echo ""

dapr run \
  --app-id order-service \
  --app-port 8006 \
  --dapr-http-port 3506 \
  --dapr-grpc-port 50006 \
  --log-level info \
  --config ./.dapr/config.yaml \
  --resources-path ./.dapr/components \
  -- dotnet run --project OrderService.Api/OrderService.Api.csproj --urls "http://localhost:8006" --environment Dapr
