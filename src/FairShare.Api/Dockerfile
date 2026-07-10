# Build context is the repo root (see docker-compose.yml) so project references resolve.

# ---------- Build ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Project files first for layer-cached restore
COPY src/FairShare.Domain/FairShare.Domain.csproj    src/FairShare.Domain/
COPY src/FairShare.Contracts/FairShare.Contracts.csproj src/FairShare.Contracts/
COPY src/FairShare.Api/FairShare.Api.csproj          src/FairShare.Api/
RUN dotnet restore src/FairShare.Api/FairShare.Api.csproj

COPY src/FairShare.Domain/    src/FairShare.Domain/
COPY src/FairShare.Contracts/ src/FairShare.Contracts/
COPY src/FairShare.Api/       src/FairShare.Api/
RUN dotnet publish src/FairShare.Api/FairShare.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

# ---------- Runtime ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# SQLite database + backups live on this volume (see docker-compose.yml)
RUN mkdir -p /data && chown "$APP_UID" /data
VOLUME /data

COPY --from=build /app/publish .

USER $APP_UID
EXPOSE 8080

ENTRYPOINT ["dotnet", "FairShare.Api.dll"]
