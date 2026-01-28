# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
WORKDIR /src

# Copy solution and project files for restore
COPY TGHarker.Identity.slnx ./
COPY TGHarker.Identity.Silo/TGHarker.Identity.Silo.csproj TGHarker.Identity.Silo/
COPY TGHarker.Identity.Grains/TGHarker.Identity.Grains.csproj TGHarker.Identity.Grains/
COPY TGHarker.Identity.Abstractions/TGHarker.Identity.Abstractions.csproj TGHarker.Identity.Abstractions/
COPY TGHarker.Identity.ServiceDefaults/TGHarker.Identity.ServiceDefaults.csproj TGHarker.Identity.ServiceDefaults/

# Restore dependencies
RUN dotnet restore TGHarker.Identity.Silo/TGHarker.Identity.Silo.csproj

# Copy source code
COPY TGHarker.Identity.Silo/ TGHarker.Identity.Silo/
COPY TGHarker.Identity.Grains/ TGHarker.Identity.Grains/
COPY TGHarker.Identity.Abstractions/ TGHarker.Identity.Abstractions/
COPY TGHarker.Identity.ServiceDefaults/ TGHarker.Identity.ServiceDefaults/

# Build and publish
WORKDIR /src/TGHarker.Identity.Silo
RUN dotnet publish -c Release -o /app/publish --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0-preview AS runtime
WORKDIR /app

# Create non-root user for security
RUN adduser --disabled-password --gecos '' appuser

# Copy published app
COPY --from=build /app/publish .

# Set environment variables
ENV ASPNETCORE_URLS=http://+:8080
ENV DOTNET_RUNNING_IN_CONTAINER=true

# Orleans ports: 11111 (silo-to-silo), 30000 (gateway for clients)
# 8080 for health endpoints
ENV ORLEANS__SILOPORT=11111
ENV ORLEANS__GATEWAYPORT=30000

# Switch to non-root user
USER appuser

EXPOSE 8080 11111 30000

ENTRYPOINT ["dotnet", "TGHarker.Identity.Silo.dll"]
