# syntax=docker/dockerfile:1

# ── Stage 1: build / publish ──────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /app

# Copy MSBuild configuration first (must be present during restore)
COPY src/Directory.Build.props src/

# Copy project files first (layer cache for restore)
COPY src/Chronith.API/Chronith.API.csproj                         src/Chronith.API/
COPY src/Chronith.Application/Chronith.Application.csproj         src/Chronith.Application/
COPY src/Chronith.Domain/Chronith.Domain.csproj                   src/Chronith.Domain/
COPY src/Chronith.Infrastructure/Chronith.Infrastructure.csproj   src/Chronith.Infrastructure/

RUN dotnet restore src/Chronith.API/Chronith.API.csproj

# Copy all source (excluding tests via .dockerignore)
COPY src/ src/

RUN dotnet publish src/Chronith.API/Chronith.API.csproj \
    -c Release \
    --no-restore \
    -o /app/out

# ── Stage 2: runtime ──────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

# Install curl for HEALTHCHECK (run as root before switching to app user)
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*

# Run as non-root (app user pre-exists in the aspnet base image)
USER app

WORKDIR /app
COPY --from=build /app/out ./

EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=5s --start-period=15s --retries=3 \
    CMD curl -f http://localhost:8080/health/live || exit 1

ENTRYPOINT ["dotnet", "Chronith.API.dll"]
