# FairShare

**FairShare** is a lightweight ASP.NET Core 8 web app that gives a quick, transparent estimate
of *who pays child support and how much* (currently just for Alabama’s guidelines). It’s
designed for fast “what-if” modeling — not for official filings or legal advice.

I began this project for my own needs. Normally, I would use the official forms from Alabama,
but the digital forms website of theirs constantly goes down for lengthy periods of time.
I wanted regular access to this form as I regularly have to do my own math to help my decision
making and so that I can double-check the legal system's work. I found that they regularly
computed the math wrong themselves and I wanted an easy, quick-check comparison when the time
came around.

I hope that this can help someone else!

> ⚠️ **Disclaimer:** This tool is for informational/educational purposes only.
It is **not legal advice** and is **not** a substitute for a licensed attorney or
court-approved worksheets.

---

## Features

- **CS-42-S (SPCA)** calculations for Alabama
  - Additional forms planned as well extending to other states.
- **Two-column responsive form** (Bootstrap 5): Plaintiff vs Defendant; collapses to stacked cards on mobile
- **Clean error experience**
  - Pretty HTML error page for browsers
  - RFC-7807 `ProblemDetails` JSON for API/fetch callers
- **Health endpoint:** `GET /healthz` for container/ingress checks
- **Container-first**: Multi-stage Dockerfile; simple `docker compose up -d` deployment

---

## Tech Stack

- **.NET 8** (ASP.NET Core MVC + Razor Views)
- **Bootstrap 5** (CDN or local)
- **Docker** & **Docker Compose**

---

## Quick Start (prebuilt image)

If you publish images (e.g., to Docker Hub or GHCR), you can run with a one-file Compose:

```yaml
# docker-compose.yml
services:
  fairshare:
    image: FairShare/fairshare:0.1.0
    environment:
      ASPNETCORE_ENVIRONMENT: "Production"
      ASPNETCORE_HTTP_PORTS: "9090"   # container listens on 9090
    ports:
      - "9090:9090"                   # host:container
    healthcheck:
      test: ["CMD", "curl", "-fsS", "http://localhost:9090/healthz"]
      interval: 10s
      timeout: 2s
      retries: 6
    restart: unless-stopped
```

```bash
docker compose up -d
# then visit:
# http://localhost:9090/
```

> **TLS:** Most setups terminate HTTPS at a reverse proxy (Nginx/Traefik). The app serves HTTP on 9090 inside the container.

---

## Build & Run from Source (local)

**Prereqs:** Docker Desktop (or Engine) and optionally the .NET 8 SDK if you want to run outside containers.

1) Clone the repo:

```bash
git clone https://github.com/youruser/fairshare.git
cd fairshare
```

2) Create a `.dockerignore` (if you don’t have one):

```text
**/bin/
**/obj/
**/.vs/
**/.vscode/
.git
.gitignore
node_modules
.env
```

3) Build & run with Compose:

```bash
docker compose build
docker compose up -d
```

4) Browse:

```
http://localhost:9090/
```

---

## Dockerfile (multi‑stage)

FairShare uses a multi‑stage Dockerfile. The runtime container listens on **9090**:

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY . .
RUN dotnet restore
RUN dotnet publish -c Release -o /app/out --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_HTTP_PORTS=9090
EXPOSE 9090

COPY --from=build /app/out .
ENTRYPOINT ["dotnet", "FairShare.dll"]   # <-- update to your assembly name if different
```

---

## Configuration

Env vars you’ll actually care about:

| Variable                   | Default        | Meaning                                           |
| -------------------------- | -------------- | ------------------------------------------------- |
| `ASPNETCORE_ENVIRONMENT` | `Production` | `Development` enables dev details on error page |
| `ASPNETCORE_HTTP_PORTS`  | `9090`       | Internal HTTP port in the container               |

**Reverse proxy note:** If you front this with Nginx/Traefik, proxy upstream to `fairshare:9090` (service name : internal port). Terminate TLS at the proxy.

---

## Health & Monitoring

- `GET /healthz` → returns `200 OK` with `"OK"` body.
  Useful for Docker/ingress health checks.

Example Compose healthcheck:

```yaml
healthcheck:
  test: ["CMD", "curl", "-fsS", "http://localhost:9090/healthz"]
  interval: 10s
  timeout: 2s
  retries: 6
```

---

## Error Handling

Global pipeline routes errors to a dedicated controller:

- Unhandled exceptions → `/error`
  - HTML page for browsers (with Trace ID)
  - `application/problem+json` for API callers
- Status codes (404/400/403/…) → `/error/{statusCode}`

Set up in `Program.cs` with `UseExceptionHandler("/error")` and `UseStatusCodePagesWithReExecute("/error/{0}")`.

---

## How the CS-42-S Calculator Works (high level)

Inputs (per parent):

- Monthly Gross Income
- Pre-existing Child Support
- Pre-existing Alimony
- Work-related Childcare Costs
- Children’s Healthcare Coverage Costs

Flow:

1. Adjusted gross income per parent → combined AGI
2. Lookup **BCSO** from the BCSO table and calculate the **Shared BCSO (SPCA)** (BCSO * 150%)
3. Add monthly childcare + healthcare expenses
4. Allocate by income share %
5. Apply shared BCSO credit (50% of SPCA) to both parents
6. Apply each parents' paid expenses to their obligation
7. Net payer & amount

> The view model holds two `ParentData` objects (`Plaintiff`, `Defendant`) and a `NumberOfChildren` field. Results of the calculation are shown in a card at the bottom after POSTing the form.

---

## Local Development (no Docker)

```bash
dotnet run --project src/FairShare/FairShare.csproj
# or
dotnet watch run --project src/FairShare/FairShare.csproj
```

Browse to `https://localhost:5001` or whatever Kestrel prints.
To align with container ports locally:

```bash
ASPNETCORE_URLS=http://localhost:9090 dotnet run
```

---

## Roadmap

- Expand to additional states/forms
- Persist scenarios (save/share a scenario link)
- Export to PDF
- Server-side and client-side currency formatting & masking
- Automated tests for table lookups and rounding rules

---

## Contributing

PRs welcome. Please:

- Keep business logic in services/managers (no UI logic in controllers)
- Add tests for calculation changes
- Keep Docker runtime on HTTP (TLS at the proxy)

Outside of PRs:

- Any mentoring or guidance is also appreciated
- Any help or guidance with specific forms/states is also appreciated!

---

## License

Licensed under the Apache License, Version 2.0. See LICENSE and NOTICE for details.

---

## Support

Issues → GitHub Issues.
For sensitive information, do **not** open a public issue.

---



## Support Me

[![Buy Me A Coffee](https://img.shields.io/badge/Buy%20me%20a%20coffee-%23FFDD00?logo=buy-me-a-coffee&logoColor=black&labelColor=%23FFDD00)](https://www.buymeacoffee.com/jmykitta)
