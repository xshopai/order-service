#!/bin/bash

# Order Service - Run with Dapr Pub/Sub

echo "Starting Order Service (Dapr Pub/Sub)..."
echo "Service will be available at: http://localhost:8006"
echo "Dapr HTTP endpoint: http://localhost:3506"
echo "Dapr gRPC endpoint: localhost:50006"
echo ""

# Kill any processes using required ports (prevents "address already in use" errors)
for PORT in 8006 3506 50006; do
    for pid in $(netstat -ano 2>/dev/null | grep ":$PORT" | grep LISTENING | awk '{print $5}' | sort -u); do
        echo "Killing process $pid on port $PORT..."
        taskkill //F //PID $pid 2>/dev/null
    done
done

dapr run \
  --app-id order-service \
  --app-port 8006 \
  --dapr-http-port 3506 \
  --dapr-grpc-port 50006 \
  --log-level info \
  --config ./.dapr/config.yaml \
  --resources-path ./.dapr/components \
  -- dotnet run --project OrderService.Api/OrderService.Api.csproj --urls "http://localhost:8006" --environment Dapr
