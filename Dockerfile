# =========================
# Build stage
# =========================
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy everything and restore
COPY . .
RUN dotnet restore

# Publish (Release)
RUN dotnet publish -c Release -o /app/out --no-restore

# =========================
# Runtime stage
# =========================
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# Install curl for healthchecks
USER root
RUN apt-get update \
 && apt-get install -y --no-install-recommends curl ca-certificates \
 && rm -rf /var/lib/apt/lists/*

# (Optional) Document the typical HTTP port; Compose will set ASPNETCORE_HTTP_PORTS
EXPOSE 9090

# App bits
COPY --from=build /app/out .

# Run
ENTRYPOINT ["dotnet", "FairShare.dll"]
