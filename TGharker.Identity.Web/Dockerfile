# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
WORKDIR /src

# Copy solution and project files for restore
COPY TGHarker.Identity.slnx ./
COPY TGharker.Identity.Web/TGharker.Identity.Web.csproj TGharker.Identity.Web/
COPY TGHarker.Identity.Abstractions/TGHarker.Identity.Abstractions.csproj TGHarker.Identity.Abstractions/
COPY TGHarker.Identity.ServiceDefaults/TGHarker.Identity.ServiceDefaults.csproj TGHarker.Identity.ServiceDefaults/

# Restore dependencies
RUN dotnet restore TGharker.Identity.Web/TGharker.Identity.Web.csproj

# Copy source code
COPY TGharker.Identity.Web/ TGharker.Identity.Web/
COPY TGHarker.Identity.Abstractions/ TGHarker.Identity.Abstractions/
COPY TGHarker.Identity.ServiceDefaults/ TGHarker.Identity.ServiceDefaults/

# Build and publish
WORKDIR /src/TGharker.Identity.Web
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

# Switch to non-root user
USER appuser

EXPOSE 8080

ENTRYPOINT ["dotnet", "TGharker.Identity.Web.dll"]
