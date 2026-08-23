![CI](https://github.com/JJWren/FairShare/actions/workflows/ci.yml/badge.svg)

# FairShare

*Lightweight child-support “what-if” calculator (currently Alabama and Oregon). A standalone **Blazor WebAssembly SPA** backed by a decoupled **REST API** (JWT auth, ASP.NET Core, SQLite).*

FairShare gives a quick, transparent estimate of who pays child support and how much under a state's guidelines — currently Alabama's CS-42 / CS-42-S and Oregon's Child Support Worksheet (OAR 137-050) — with line-by-line parity against the official worksheets.

> ⚠️ Disclaimer: Informational/educational only. Not legal advice. Not a substitute for an attorney or court-approved worksheets.

---

## Release history

See [`CHANGELOG.md`](CHANGELOG.md) — maintained automatically by release-please, so it is always current.

---

## Features

- **State Support (Alabama)**:
  - CS-42 (Rev. 5/2022, standard custody) and CS-42-S (Eff. 6/2023, shared 50/50 physical custody).
  - Line-by-line parity with the official AOC Excel worksheets — every numbered line comes back with the result (see `CONTEXT.md` and `docs/adr/`).
  - Switch between the two forms on the calculator page without re-entering figures; the full worksheet renders under the result.
  - Export the completed **official AOC Excel workbook** for the form you are viewing (inputs typed into its cells, formulas left live).
- **State Support (Oregon, beta)**:
  - The single Child Support Worksheet (CSF 02 0910, OAR 137-050-0700 to -0765): all custody arrangements via the overnights-based parenting time credit, Children Attending School (support to 21, paid directly to the child — both parents can owe at once), the medical-support block with rule-based coverage selection, the $100 minimum order, and the SS/VA offset.
  - Line-by-line parity with the official DOJ Guidelines Calculator workbook, pinned by golden cases read back from the workbook and ClosedXML oracle tests that evaluate the state's own formulas.
  - An overnights pattern-builder (preset schedules counted on a simulated two-year calendar), a plain-language guide at `/guides/oregon-worksheet`, and a court-prep card (the 17 OAR 137-050-0760 rebuttal factors and the ±15% agreed-amount band).
  - Every estimate names the rule vintage it implements ("Implements OAR 137-050 effective 2026-07-01") and links the official state calculator. Excel export for Oregon is not available yet.
- **Scenarios**: Save a worksheet's complete figures as a named Scenario and reload it later — reopening recomputes under the current rules and says so when the number moved ([ADR 0006](docs/adr/0006-scenario-recompute-with-notice.md)).
- **Guest-first**: Visitors land on the state picker as guests — no account needed to calculate or export. Saving is the gated feature that invites sign-in ([ADR 0002](docs/adr/0002-guest-first-landing.md)).
- **Accounts via Google sign-in**: Free public accounts use Google OAuth only — FairShare never holds a password for them ([ADR 0004](docs/adr/0004-external-oauth-only-public-accounts.md)). Opt-in "remember this device", guest work carried over on sign-in (saved only on an explicit yes), and self-service **hard delete** from the Account page.
- **Data Persistence**: Save and manage Parent Profiles (Plaintiff vs Defendant). Within your saved parents the display name is the natural key: re-saving an existing name (even with adjusted figures) updates that record in place instead of creating a same-named duplicate.
- **Privacy-first analytics**: First-party, cookieless, content-free page views and events; Do Not Track / Global Privacy Control honored by recording nothing ([ADR 0003](docs/adr/0003-first-party-cookieless-analytics.md)). Disclosed in full on the in-app `/privacy` page.
- **Admin observability**: `/admin/stats` dashboard, persistent diagnostic logs and audit trail at `/admin/logs`, and a verbose-logging mode that always turns itself back off.
- **Donations**: Optional `/support` page with a first-party `/go/donate` redirect — hidden entirely unless `DONATE_URL` is configured.
- **Responsive UI**: Two-column layout optimized for both desktop and mobile (Bootstrap 5), with a Light/Dark/Auto theme toggle.
- **Health & Safety**: Integrated database integrity checks and automated backup zipping on startup.

---

## Roles & Permissions

| Role      | Typical Access       | Notes                                                                 |
| --------- | --------------------- | ---------------------------------------------------------------------|
| **Guest** | Default identity      | Every visitor until they sign in; run calculations and export, no saving or admin. |
| **User**  | Normal app usage      | Signs in with Google; create and run scenarios, save parent profiles. |
| **Admin** | Full administration   | Manage users and roles, view the stats dashboard, logs, and audit trail, toggle verbose logging. Local sign-in with a mandatory authenticator (TOTP) code. |

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
- **Backend**: ASP.NET Core Web API
- **Auth**: JWT bearer (access + rotating refresh tokens); Google OAuth for public accounts; TOTP two-factor for admin accounts
- **Shared Logic**: .NET class libraries (Domain calculators, Contracts DTOs)
- **Database**: SQLite (EF Core) — also backs the first-party analytics, diagnostic logs, and audit trail
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

If `ADMIN_PASSWORD` was left empty, the generated admin password is printed once in `docker compose logs api`. The SQLite database (and pre-migration backups) persist in the named `fairshare-data` volume across restarts. Both images build from source — no registry needed.

That is the whole happy path — a local instance with admin-created accounts only. Everything beyond it lives in the **[setup guide](docs/SETUP.md)**:

- **Google sign-in** — the only public sign-up path; without it, no one can create an account
- **Donations** (`DONATE_URL` / the `/support` page)
- The complete `.env` and configuration reference, including what happens when each key is absent
- Prebuilt GHCR images, reverse-proxy/TLS hosting, and hardening a public instance

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

An admin account with TOTP enrolled gets `401` with `"requiresTwoFactor": true` — repeat the login with a `twoFactorCode` field. Swagger UI is available at `/swagger` in Development. The full endpoint reference — auth flow, request/response bodies, error shapes, and rate-limit behavior — is in [`docs/API.md`](docs/API.md), and a ready-to-import Postman collection (chained auth, sample bodies, assertions) is at [`docs/FairShare.postman_collection.json`](docs/FairShare.postman_collection.json).

---

## Configuration

The complete configuration reference — every `.env` variable and API setting, with defaults and the behavior when each is absent — is in [`docs/SETUP.md`](docs/SETUP.md). For bare-`dotnet run` development the ones that matter are `Jwt:SigningKey` (required) and the web app's `Api:BaseUrl`.

---

## Testing

```bash
dotnet test FairShare.sln
```

`FairShare.Tests` covers the calculators (`FairShare.Domain`) — including golden cases read back from the official AOC workbooks — and the auth/catalog/calculation endpoints end-to-end against an in-memory-configured instance of `FairShare.Api` (`WebApplicationFactory`).

---

## Contributing

Keep calculation logic in `FairShare.Domain` and wire types in `FairShare.Contracts` — both are referenced by the API and (Contracts only) by the Web app, so changes there are automatically shared.

---

## License

Apache 2.0. See [`LICENSE`](LICENSE), and [`NOTICE`](NOTICE) for third-party components and the embedded official AOC worksheet workbooks.

---

## Support

Issues → [GitHub Issues](https://github.com/JJWren/FairShare/issues). A deployed instance also serves an in-app `/support` page explaining the project's costs and how to help with them.

### Enjoy my work?
[![Buy Me A Coffee](https://img.shields.io/badge/Buy%20me%20a%20coffee-%23FFDD00?logo=buy-me-a-coffee&logoColor=black&labelColor=%23FFDD00)](https://www.buymeacoffee.com/jmykitta)
