# =============================================================================
# Multi-stage Dockerfile for .NET Order Service
# =============================================================================

# -----------------------------------------------------------------------------
# Base stage - Common setup for all stages
# -----------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app

# Install system dependencies (wget for healthcheck)
RUN apt-get update && apt-get install -y \
    wget \
    && rm -rf /var/lib/apt/lists/*

# Create non-root user
RUN groupadd -r orderuser && useradd -r -g orderuser orderuser

# -----------------------------------------------------------------------------
# Build stage - Build the application
# -----------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution and project files
COPY ["OrderService.sln", "./"]
COPY ["OrderService.Core/OrderService.Core.csproj", "OrderService.Core/"]
COPY ["OrderService.Api/OrderService.Api.csproj", "OrderService.Api/"]
COPY ["OrderService.Tests/OrderService.Tests.csproj", "OrderService.Tests/"]

# Restore dependencies
RUN dotnet restore "OrderService.sln"

# Copy source code
COPY . .

# Build
WORKDIR "/src/OrderService.Api"
RUN dotnet build "OrderService.Api.csproj" -c Release -o /app/build

# -----------------------------------------------------------------------------
# Publish stage - Publish the application
# -----------------------------------------------------------------------------
FROM build AS publish
RUN dotnet publish "OrderService.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# -----------------------------------------------------------------------------
# Development stage - For local development
# -----------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS development
WORKDIR /src

# Note: In development, mount code as volume for hot reload
# Install system dependencies (wget for healthcheck)
RUN apt-get update && apt-get install -y \
    wget \
    && rm -rf /var/lib/apt/lists/*

# Copy solution and project files
COPY ["OrderService.sln", "./"]
COPY ["OrderService.Core/OrderService.Core.csproj", "OrderService.Core/"]
COPY ["OrderService.Api/OrderService.Api.csproj", "OrderService.Api/"]
COPY ["OrderService.Tests/OrderService.Tests.csproj", "OrderService.Tests/"]

# Restore dependencies
RUN dotnet restore "OrderService.sln"

# Copy source code
COPY . .

# Create non-root user
RUN groupadd -r orderuser && useradd -r -g orderuser orderuser
RUN chown -R orderuser:orderuser /src
USER orderuser

# Health check (using wget which is smaller than curl)
HEALTHCHECK --interval=30s --timeout=10s --start-period=60s --retries=3 \
    CMD wget --no-verbose --tries=1 --spider http://localhost:1006/readiness || exit 1

# Expose port
EXPOSE 1006

# Run in development mode with hot reload
WORKDIR "/src/OrderService.Api"
ENTRYPOINT ["dotnet", "watch", "run", "--project", "OrderService.Api.csproj", "--urls", "http://0.0.0.0:1006"]

# -----------------------------------------------------------------------------
# Production stage - Optimized for production deployment
# -----------------------------------------------------------------------------
FROM base AS production

# Copy published app
COPY --from=publish --chown=orderuser:orderuser /app/publish .

# Remove unnecessary files for production
RUN rm -rf /tmp/* /var/tmp/*

# Switch to non-root user
USER orderuser

# Health check (using wget which is smaller than curl)
HEALTHCHECK --interval=30s --timeout=10s --start-period=60s --retries=3 \
    CMD wget --no-verbose --tries=1 --spider http://localhost:1006/readiness || exit 1

# Expose port
EXPOSE 1006

# Configure ASP.NET Core
ENV ASPNETCORE_URLS=http://+:1006
ENV ASPNETCORE_ENVIRONMENT=Production

# Entry point
ENTRYPOINT ["dotnet", "OrderService.Api.dll"]

# Labels for better image management and security scanning
LABEL maintainer="xshop.ai Team"
LABEL service="order-service"
LABEL version="1.0.0"
LABEL org.opencontainers.image.source="https://github.com/xshopai/xshopai"
LABEL org.opencontainers.image.description="Order Service for xshop.ai platform"
LABEL org.opencontainers.image.vendor="xshop.ai"
LABEL framework="aspnetcore"
LABEL language="csharp"
