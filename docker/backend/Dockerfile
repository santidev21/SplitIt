# Multi-stage Dockerfile for SplitIt .NET 8 Web API
# Build stage using .NET 9 SDK (backwards-compatible with net8.0 targets,
# and matches EF Core 9.x tooling for proper migration discovery)
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy solution and project files first to leverage Docker layer caching for restore
COPY SplitIt.API/SplitIt.Back.sln SplitIt.API/
COPY SplitIt.API/SplitIt.API/SplitIt.API.csproj SplitIt.API/SplitIt.API/
COPY SplitIt.API/SplitIt.Application/SplitIt.Application.csproj SplitIt.API/SplitIt.Application/
COPY SplitIt.API/SplitIt.Domain/SplitIt.Domain.csproj SplitIt.API/SplitIt.Domain/
COPY SplitIt.API/SplitIt.Infrastructure/SplitIt.Infrastructure.csproj SplitIt.API/SplitIt.Infrastructure/
COPY SplitIt.API/SplitIt.Shared/SplitIt.Shared.csproj SplitIt.API/SplitIt.Shared/
COPY SplitIt.Tests/SplitIt.Tests.csproj SplitIt.Tests/

RUN dotnet restore SplitIt.API/SplitIt.Back.sln

# Copy all source code
COPY SplitIt.API/ SplitIt.API/

# Build and publish in Release mode
WORKDIR /src/SplitIt.API/SplitIt.API
RUN dotnet publish SplitIt.API.csproj -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage using minimal ASP.NET image
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS final
WORKDIR /app

# Install wget and icu-libs for healthcheck and EF Core database migration support
RUN apk add --no-cache wget icu-libs && apk upgrade --no-cache

ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false \
    ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production

# Prepare DataProtection keys directory for non-root persistence (volume mounted at runtime)
RUN mkdir -p /home/app/.aspnet/DataProtection-Keys && chown -R 1654:1654 /home/app/.aspnet

# Set up non-root execution (app user UID 1654 in .NET 8 images)
USER $APP_UID

# Expose HTTP port 8080 (standard non-root port for .NET 8)
EXPOSE 8080

COPY --from=build /app/publish .

# Healthcheck targeting liveness (not DB-dependent) to avoid restart loop when DB is down
HEALTHCHECK --interval=30s --timeout=5s --start-period=15s --retries=3 \
  CMD wget --no-verbose --tries=1 --spider http://localhost:8080/health/live || exit 1

ENTRYPOINT ["dotnet", "SplitIt.API.dll"]
