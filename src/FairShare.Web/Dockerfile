# Build context is the repo root (see docker-compose.yml) so project references resolve.

# ---------- Build ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Project files first for layer-cached restore
COPY src/FairShare.Contracts/FairShare.Contracts.csproj src/FairShare.Contracts/
COPY src/FairShare.Web/FairShare.Web.csproj             src/FairShare.Web/
RUN dotnet restore src/FairShare.Web/FairShare.Web.csproj

COPY src/FairShare.Contracts/ src/FairShare.Contracts/
COPY src/FairShare.Web/       src/FairShare.Web/
RUN dotnet publish src/FairShare.Web/FairShare.Web.csproj -c Release -o /app/publish

# ---------- Runtime (static files via nginx) ----------
FROM nginx:alpine AS runtime

COPY --from=build /app/publish/wwwroot /usr/share/nginx/html
COPY src/FairShare.Web/nginx.conf /etc/nginx/conf.d/default.conf
# .inc extension keeps it out of nginx's conf.d/*.conf auto-include; nginx.conf includes it.
COPY src/FairShare.Web/fairshare-security-headers.inc /etc/nginx/conf.d/fairshare-security-headers.inc
# nginx's stock entrypoint runs every script in /docker-entrypoint.d before serving.
COPY src/FairShare.Web/docker-entrypoint.d/40-fairshare-appsettings.sh /docker-entrypoint.d/40-fairshare-appsettings.sh
COPY src/FairShare.Web/docker-entrypoint.d/50-fairshare-csp.sh /docker-entrypoint.d/50-fairshare-csp.sh

RUN rm -f /usr/share/nginx/html/appsettings.Development.json \
 && chmod +x /docker-entrypoint.d/40-fairshare-appsettings.sh /docker-entrypoint.d/50-fairshare-csp.sh

EXPOSE 80
