#!/bin/bash

# Order Service - Run without Dapr (local development)

echo "Starting Order Service (without Dapr)..."
echo "Service will be available at: http://localhost:8006"
echo ""
echo "Note: Event publishing and service-to-service calls will fail without Dapr."
echo "This mode is suitable for isolated development and testing."
echo ""

# Navigate to the API project directory
cd OrderService.Api

# Run with dotnet
dotnet run
