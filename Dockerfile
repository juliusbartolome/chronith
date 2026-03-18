# syntax=docker/dockerfile:1

# ── Stage 1: build / publish ──────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /app

# Pin SDK version to match global.json (prevents MSBuild glob expansion
# issues caused by backslash path separators on Linux in newer SDK patches)
COPY global.json .

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

# MSBuild's default **/*.cs glob traverses bin/ and obj/ subdirectories.
# .dockerignore strips those directories from the context, so MSBuild throws
# an AggregateException trying to enumerate a missing path and falls back to
# treating "**/*.cs" as a literal filename — causing CS2021/CS2001.
# Pre-creating the expected directories prevents that traversal failure.
RUN find /app/src -name "*.csproj" -exec dirname {} \; | \
    xargs -I{} sh -c 'mkdir -p "{}/bin/Debug" "{}/bin/Release" "{}/obj"'

# Build first so bin/Release exists before publish runs incremental glob expansion
RUN dotnet build src/Chronith.API/Chronith.API.csproj \
    -c Release \
    --no-restore

RUN dotnet publish src/Chronith.API/Chronith.API.csproj \
    -c Release \
    --no-build \
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

LABEL org.opencontainers.image.source="https://github.com/juliusbartolome/chronith"
LABEL org.opencontainers.image.description="Chronith booking engine API"
LABEL org.opencontainers.image.licenses="MIT"

ENTRYPOINT ["dotnet", "Chronith.API.dll"]
