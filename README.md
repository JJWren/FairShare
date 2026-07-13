![CI](https://github.com/JJWren/FairShare/actions/workflows/ci.yml/badge.svg)

# FairShare

*Lightweight child-support “what-if” calculator (currently Alabama). A standalone **Blazor WebAssembly SPA** backed by a decoupled **REST API** (JWT auth, ASP.NET Core, SQLite).*

FairShare gives a quick, transparent estimate of who pays child support and how much under Alabama’s guidelines (CS-42 / CS-42-S).

> ⚠️ Disclaimer: Informational/educational only. Not legal advice. Not a substitute for an attorney or court-approved worksheets.

---

## What’s new in v2

- **Public hardening (2.0.0)**: Per-IP rate limiting (strict budget on the auth endpoints), self-registration disabled by default (**breaking** — set `ALLOW_SELF_REGISTRATION=true` to restore sign-ups), self-service change-password and admin password reset (both revoke all of the user's sessions), automatic refresh-token cleanup, and CSP/security headers on the web container.
- **Architecture Overhaul**: Split into a standalone Blazor WebAssembly SPA (`FairShare.Web`) and an independent REST API (`FairShare.Api`), replacing the previous hybrid server-hosted-WASM app.
- **JWT Auth**: Cookie-based ASP.NET Identity replaced with JWT bearer auth (access + rotating refresh tokens), so the API is directly usable from curl/Postman/any CLI — not just the browser.
- **Server-side Calculations**: Calculation logic moved behind `POST /api/v1/states/{state}/forms/{form}/calculations`, so results are consistent regardless of client.
- **Theme Toggle**: Light/Dark/Auto, persisted in `localStorage`, applied pre-render.
- **Saved Parent Profiles Page**: Standalone `/profiles` page to rename/archive saved profiles, in addition to inline reuse from the calculator.
- **Two-container Docker Compose**: `docker compose up --build` runs the API (with a persistent SQLite volume) and the nginx-served Web app; bare `dotnet run` still works too.

---

## Features

- **State Support (Alabama)**:
  - CS-42 (Standard) calculations.
  - CS-42-S (Shared Parenting/SPCA) calculations.
- **Responsive UI**: Two-column layout optimized for both desktop and mobile (Bootstrap 5).
- **Theme Support**: Light/Dark/Auto theme toggle.
- **Data Persistence**: Save and manage Parent Profiles (Plaintiff vs Defendant). Within your saved parents the display name is the natural key: re-saving an existing name (even with adjusted figures) updates that record in place instead of creating a same-named duplicate.
- **Admin Tools**: Comprehensive user management and automated database seeding.
- **Health & Safety**: Integrated database integrity checks and automated backup zipping on startup.
- **Guest mode**: Try the calculator without creating an account (no saving).

---

## Roles & Permissions

| Role      | Typical Access       | Notes                                                                 |
| --------- | --------------------- | ---------------------------------------------------------------------|
| **Guest** | Limited preview       | Run calculations; no saving or admin. (`Continue as Guest` on login) |
| **User**  | Normal app usage      | Create and run scenarios, save parent profiles.                      |
| **Admin** | Full administration   | Manage users, roles, and access the users dashboard.                 |

---

## Solution Layout

| Project | Type | Responsibility |
|---|---|---|
| [`FairShare.Domain`](src/FairShare.Domain/README.md) | classlib | Pure calculation engine — calculators, state/form catalog. No EF/Identity/ASP.NET. |
| [`FairShare.Contracts`](src/FairShare.Contracts/README.md) | classlib | Wire DTOs shared by the API and the Web app (auth, calculation, parents, admin). |
| [`FairShare.Api`](src/FairShare.Api/README.md) | ASP.NET Core Web API | JWT auth, EF Core + SQLite persistence, all controllers. |
| [`FairShare.Web`](src/FairShare.Web/README.md) | Blazor WebAssembly | Standalone SPA calling the API over HTTP/JSON with a JWT bearer token. |
| [`FairShare.Tests`](src/FairShare.Tests/README.md) | xUnit | Calculator unit tests + API integration tests (`WebApplicationFactory`). |

Each project has its own README with conventions and extension points; the full endpoint reference lives in [`docs/API.md`](docs/API.md).

---

## Tech Stack

- **Frontend**: Blazor WebAssembly (.NET 10, standalone)
- **Backend**: ASP.NET Core Web API, JWT bearer auth
- **Shared Logic**: .NET class libraries (Domain calculators, Contracts DTOs)
- **Database**: SQLite (EF Core)
- **Styling**: Bootstrap 5
- **Deployment**: Docker Compose (API container + nginx container for the SPA), or bare `dotnet run`

---

## Quick Start (Docker Compose)

Requires Docker with Compose v2.

```bash
cp .env.example .env
# Edit .env: set JWT_SIGNING_KEY (e.g. `openssl rand -base64 48`) and optionally ADMIN_PASSWORD

docker compose up --build
```

- Web app: http://localhost:5858
- API: http://localhost:5859 (`/healthz`; Swagger is **Development-only** and not served by the Production compose build — use the bare `dotnet run` setup below to browse it)

If `ADMIN_PASSWORD` was left empty, the generated admin password is printed once in `docker compose logs api`. The SQLite database (and pre-migration backups) persist in the named `fairshare-data` volume across restarts.

Both images build from source — no registry needed. Ports and browser-visible URLs are configurable in `.env` (`WEB_PORT`/`API_PORT`/`WEB_ORIGIN`/`API_BASE_URL`).

**Hosting behind a reverse proxy (VPS):** terminate TLS at your proxy and forward `X-Forwarded-Proto` (the API honors it for cookie security attributes), set `WEB_ORIGIN` to the web app's public URL (CORS) and `API_BASE_URL` to the API's public URL. `API_BASE_URL` must always be the *browser-visible* API URL, never the compose-internal service name. Note that rate limiting keys on the direct peer IP: behind a reverse proxy every client collapses into the proxy's bucket. That is deliberate — trusting `X-Forwarded-For` without pinning the proxy in `KnownProxies` would let clients spoof their way out of throttling — so pin your proxy before switching the limiter to forwarded addresses.

### Hardening a public instance

- **Self-registration is disabled by default** (`ALLOW_SELF_REGISTRATION=false`): create accounts from **Admin → Users**. Only enable it if you want strangers to be able to sign up.
- **Admin bootstrap:** set a strong `ADMIN_PASSWORD` in `.env` before first boot. If you let the seeder generate one, treat it as burned — Docker persists container logs — so log in, change it from the **Account** page, and consider renaming the account (`ADMIN_USER`); every credential-stuffing bot tries `admin` first.
- **After first boot**, set `ADMIN_SEED_ENABLED=false` and remove `ADMIN_PASSWORD` from `.env` — the seeder only matters once.
- **Signing key:** generate a fresh `JWT_SIGNING_KEY` for production (`openssl rand -base64 48`); never reuse a key that has been committed anywhere. Rotating it only invalidates outstanding access tokens (≤30 min); sessions recover silently via the refresh cookie.
- **Passwords:** users change their own via **Account → Change password**; admins reset others' via **Admin → Users → Edit**. Both revoke all of that user's refresh tokens.

---

## Quick Start (bare `dotnet run`)

Requires the .NET 10 SDK.

```bash
# Terminal 1 — the API
cd src/FairShare.Api
dotnet run

# Terminal 2 — the web app
cd src/FairShare.Web
dotnet run
```

By default the API listens on `https://localhost:7080` / `http://localhost:5080` and the web app on `https://localhost:7090` / `http://localhost:5090` (see `Properties/launchSettings.json` in each project). The web app's `wwwroot/appsettings.Development.json` points `Api:BaseUrl` at the API; the API's `appsettings.Development.json` lists the web app's origins under `Cors:AllowedOrigins`. Update both if you change ports.

On first run the API seeds an `admin` account — check the console output for the generated password (or set `AdminSeed:Password` yourself) and the SQLite database (`fairshare.db`) is created and migrated automatically.

### Using the API directly

The API is a normal JWT-secured REST API — no browser required:

```bash
# Get a token
curl -X POST http://localhost:5080/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{"userName":"admin","password":"<seeded-password>"}'

# Use it
curl http://localhost:5080/api/v1/states \
  -H "Authorization: Bearer <accessToken>"
```

Swagger UI is available at `/swagger` in Development. The full endpoint reference — auth flow, request/response bodies, error shapes, and rate-limit behavior — is in [`docs/API.md`](docs/API.md), and a ready-to-import Postman collection (chained auth, sample bodies, assertions) is at [`docs/FairShare.postman_collection.json`](docs/FairShare.postman_collection.json).

---

## Configuration

| Setting (API)                      | Default      | Purpose                                                                                |
| ----------------------------------- | ------------ | -------------------------------------------------------------------------------------- |
| `ConnectionStrings:Default`         | —            | SQLite connection string.                                                              |
| `Jwt:SigningKey`                    | —            | HMAC-SHA256 signing key for access tokens. Required; set via user-secrets/env var in real deployments. |
| `Jwt:AccessTokenMinutes`            | `30`         | Access token lifetime.                                                                 |
| `Jwt:RefreshTokenDays`              | `30`         | Refresh token lifetime.                                                                |
| `Cors:AllowedOrigins`               | `[]`         | Origins allowed to call the API (the Web app's URL).                                   |
| `AdminSeed:Enabled`                 | `true`       | Enables seeding the initial admin account. Disable after first boot.                   |
| `AdminSeed:User`                    | `admin`      | Username for the initial admin.                                                        |
| `AdminSeed:Password`                | *(random)*   | Password for the initial admin (logged on first run if empty).                         |
| `AdminSeed:LogGeneratedPassword`    | `true`       | Whether a generated admin password is printed to the log.                              |
| `Auth:AllowSelfRegistration`        | `false`      | Whether `POST /api/v1/auth/register` is open. Off = admin creates accounts.            |
| `RateLimiting:Enabled`              | `true`       | Kill-switch for rate limiting (values are fixed: 100 req/min per IP globally, 10 req/min per IP on the auth endpoints). |

| Setting (Web)     | Default | Purpose                                    |
| ------------------ | ------- | ------------------------------------------ |
| `Api:BaseUrl`       | —       | Base URL of `FairShare.Api` to call.       |

---

## Testing

```bash
dotnet test FairShare.sln
```

`FairShare.Tests` covers the CS-42 calculator (`FairShare.Domain`) and the auth/catalog endpoints end-to-end against an in-memory-configured instance of `FairShare.Api` (`WebApplicationFactory`).

---

## Contributing

Keep calculation logic in `FairShare.Domain` and wire types in `FairShare.Contracts` — both are referenced by the API and (Contracts only) by the Web app, so changes there are automatically shared.

---

## License

Apache 2.0. See `LICENSE`.

---

## Support

Issues → [GitHub Issues](https://github.com/JJWren/FairShare/issues).

### Enjoy my work?
[![Buy Me A Coffee](https://img.shields.io/badge/Buy%20me%20a%20coffee-%23FFDD00?logo=buy-me-a-coffee&logoColor=black&labelColor=%23FFDD00)](https://www.buymeacoffee.com/jmykitta)
