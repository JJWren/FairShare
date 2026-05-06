# =========================
# Build stage
# =========================
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy all project files first for efficient caching
COPY ["FairShare.sln", "./"]
COPY ["FairShare.Server/FairShare.Server.csproj", "FairShare.Server/"]
COPY ["FairShare.Client/FairShare.Client.csproj", "FairShare.Client/"]
COPY ["FairShare.Shared/FairShare.Shared.csproj", "FairShare.Shared/"]

# Restore all projects via the solution file
RUN dotnet restore FairShare.sln

# Copy everything else and build the solution
COPY . .
RUN dotnet build FairShare.sln -c Release

# Publish the Server project (Release)
# This will include the pre-built Client project assets
FROM build AS publish
RUN dotnet publish "FairShare.Server/FairShare.Server.csproj" -c Release -o /app/publish /p:UseAppHost=false

# =========================
# Runtime stage
# =========================
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Install curl for healthchecks
USER root
RUN apt-get update \
 && apt-get install -y --no-install-recommends curl ca-certificates \
 && rm -rf /var/lib/apt/lists/*

# (Optional) Document the typical HTTP port; Compose will set ASPNETCORE_HTTP_PORTS
EXPOSE 9090

# App bits
COPY --from=publish /app/publish .

# Run
ENTRYPOINT ["dotnet", "FairShare.Server.dll"]
