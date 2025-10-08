![CI](https://github.com/JJWren/FairShare/actions/workflows/ci.yml/badge.svg)

# FairShare

*Lightweight child-support “what-if” calculator (currently Alabama). Runs anywhere with Docker. Built with ASP.NET Core, SQLite, and Bootstrap.*

FairShare gives a quick, transparent estimate of who pays child support and how much under Alabama’s guidelines (CS-42 / CS-42-S). It’s meant for fast scenario modeling — not official filings or legal advice.

> ⚠️ Disclaimer: Informational/educational only. Not legal advice. Not a substitute for an attorney or court-approved worksheets. 

**Live demo:** https://fairshare.theguywiththedogs.dev

Demo credentials are displayed on the login page: `demo` / `Demo@123456!` (*limited access*). A Guest entry link is also present.

### Examples:

<figure>
    <img width="1341" height="488" alt="login screen" src="https://github.com/user-attachments/assets/0b584554-9fbd-4dcc-a7a4-0d850c1db698" />
    <figcaption><strong>Figure 1.</strong> Login View</figcaption>
</figure>
<br>
<br>

<figure>
    <img width="1319" height="389" alt="landing page - choose state" src="https://github.com/user-attachments/assets/0de528a9-c411-4536-875c-9a1a5051a0f0" />
    <strong>Figure 2.</strong> Admin Landing View - Choose State
</figure>
<br>
<br>

<figure>
    <img width="1327" height="450" alt="state selection" src="https://github.com/user-attachments/assets/0b957e30-311f-444f-a337-2a8c12e25642" />
    <strong>Figure 3.</strong> Select Form for State
</figure>
<br>
<br>

<figure>
    <img width="1340" height="902" alt="image" src="https://github.com/user-attachments/assets/72ca4a63-9290-41ef-873e-0533940b6c12" />
    <strong>Figure 4.</strong> Example Form in Wide View
</figure>
<br>
<br>

<figure>
    <img width="1067" height="848" alt="image" src="https://github.com/user-attachments/assets/08de33d5-1aa4-4ca2-ac61-cad8c23673d7" />
    <strong>Figure 5.</strong> Example Form in Reduced View (iPhone 14 Pro Max)
</figure>
<br>
<br>

<figure>
    <img width="1317" height="372" alt="image" src="https://github.com/user-attachments/assets/bd2bf91a-b53a-4a1d-a08d-9f18df94158b" />
    <strong>Figure 6.</strong> Parent Profiles View:
</figure>
<br>
<br>

<figure>
    <img width="1311" height="543" alt="image" src="https://github.com/user-attachments/assets/27884b52-b731-45dc-8cd8-afe1f7946b85" />
    <strong>Figure 7.</strong> Settings (account, manage users)
</figure>
<br>
<br>

---

## What’s new in v4

- Accounts, roles & authorization. Admins can manage users (*create/edit admins and users, delete accounts*) and view the users screen; demo/guest are intentionally limited. (*See Roles & Permissions below.*)

- README and setup clarified; quick links to health checks and container usage.

- For full release notes see 4.0.0 on GitHub Releases.

---

## Features

- For Alabama, Two-column, responsive form (Plaintiff vs Defendant) with concise results:
  - CS-42 (Standard) calculations
  - CS-42-S (SPCA) calculations

- Light/Dark/Auto theme toggle.
- Clean error experience: pretty HTML for browsers; RFC-7807 ProblemDetails for API/fetch callers. 
- Health endpoint: `GET /healthz` for container/ingress checks.
- Container-first: multi-stage Dockerfile; dead-simple `docker compose up -d`.
- Inputs per parent:
  - monthly gross
  - pre-existing child support
  - pre-existing alimony
  - work-related childcare
  - children’s health coverage.
- Follows the guidelines from Alabama's Rule 32: https://judicial.alabama.gov/docs/library/rules/ja32.pdf

---

## Roles & Permissions (v4)
| Role      | Typical Access                  | Notes                                                                                                                                  |
| --------- | ------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------- |
| **Guest** | Limited preview                 | Navigate basic UI; no privileged actions. (Entry link on login page.) ([fairshare.theguywiththedogs.dev][1])                           |
| **Demo**  | Limited, read-only/guard-railed | Login shown on the site; intended for safe exploration. ([fairshare.theguywiththedogs.dev][1])                                         |
| **User**  | Normal app usage                | Create and run scenarios within allowed scope (no user admin). *(Scope may evolve with future features.)*                              |
| **Admin** | Full user administration        | Create/edit **Admin** and **User** accounts, delete accounts, and access the users screen for management. *(New in v4.)* ([GitHub][2]) |

The live demo enforces stricter limits on Guest/Demo by design.

---

## Tech Stack

.NET (ASP.NET Core MVC with standard controller/views + Razor pages for account management)
Bootstrap 5
SQLite for persistence (simple, portable)
Docker & Docker Compose for deployment

---

## Quick Start (Docker)

Create directory for FairShare

Create a one-file compose and go:

```bash
# docker-compose.yml
services:
  fairshare:
    image: ghcr.io/jjwren/fairshare:latest
    environment:
      ASPNETCORE_ENVIRONMENT: "Production"
      ASPNETCORE_HTTP_PORTS: "9090"   # container listens on 9090
      UI__DefaultTheme: "dark"        # light|dark|auto
      AdminSeed__Enabled: "true"      # this pulls the initial admin config from the docker-compose/env that you will create for this
      AdminSeed__User: "${AdminSeed__User}"    # this should be set in an .env file
      AdminSeed__Password: "${AdminSeed__Password}"
      AdminSeed__LogGeneratedPassword: "true"
    ports:
      - "9090:9090"
    healthcheck:
      test: ["CMD", "curl", "-fsS", "http://localhost:9090/healthz"]
      interval: 10s
      timeout: 2s
      retries: 6
    restart: unless-stopped
```


Then:

```bash
cd fairshare
docker pull
docker compose up -d
# open http://localhost:9090
```

***TLS tip:** terminate HTTPS at your reverse proxy (Nginx/Traefik) and proxy upstream to fairshare:9090.*

---

## Build from Source

> ***Prereqs:** Docker Desktop (or Engine). .NET SDK optional if you want to run outside containers.*

```bash
git clone https://github.com/JJWren/FairShare.git
cd FairShare
docker compose build
docker compose up -d
# http://localhost:9090
```


Running without Docker:

# choose the actual csproj path used by the solution
`dotnet run --project src/FairShare/FairShare.csproj`
# or:
`ASPNETCORE_URLS=http://localhost:9090 dotnet run --project src/FairShare/FairShare.csproj`


(Port alignment helps match container defaults.) 

---

## Configuration

Environment variables you’ll actually care about:

| Variable                           | Default             | Purpose                                                                                |
| ---------------------------------- | ------------------- | -------------------------------------------------------------------------------------- |
| `ASPNETCORE_ENVIRONMENT`           | `Production`        | Use `Development` to show detailed errors locally                                      |
| `ASPNETCORE_HTTP_PORTS`            | `9090`              | Internal HTTP port inside the container                                                |
| `AdminSeed__Enabled`               | `true`              | Enables seeding the admin account from docker. Do not change on initial setup.         |
| `AdminSeed__User`                  | `set this yourself` | Use an `.env` file or set this here                                                    |
| `AdminSeed__Password`              | `set this yourself` | Use an `.env` file or set this here                                                    |
| `AdminSeed__LogGeneratedPassword`  | `true`              | If you do not set the password, the app generates one for one and logs it on first run |


***Reverse proxy:** point upstream to `fairshare:9090.` Terminate TLS at the proxy.*

---

## Health & Monitoring

`GET /healthz` → returns `200 OK` and `"OK"` body; use in Docker/ingress health checks.

Example:

```curl
healthcheck:
  test: ["CMD", "curl", "-fsS", "http://localhost:9090/healthz"]
  interval: 10s
  timeout: 2s
  retries: 6
```

---

## Roadmap

- Additional states/forms beyond Alabama that also do not have official calculators:
  - Florida
  - Mississippi
  - West Virginia
- Persisted scenarios (save/share links)
- xlsx/pdf export
- Automated tests for table lookups & rounding rules 

---

## Contributing

PRs welcome. Please keep business logic in services/managers, add tests for calculation changes, and keep runtime HTTP (terminate TLS at proxy). Mentorship/feedback on code, architecture, etc. is appreciated. 

---

## License

Apache 2.0. See `LICENSE` and `NOTICE`.

---

## Support

Issues → GitHub Issues (avoid posting sensitive information).

### If this at all helped you or you enjoy my work, consider giving a tired soul a coffee!

[![Buy Me A Coffee](https://img.shields.io/badge/Buy%20me%20a%20coffee-%23FFDD00?logo=buy-me-a-coffee&logoColor=black&labelColor=%23FFDD00)](https://www.buymeacoffee.com/jmykitta)

---

## Notes on versions

`v4.0.0` introduces user management & authorization (breaking change for deployments that had no auth). Review roles and ensure an admin path to manage accounts. 
GitHub

`v2.0.0` I believe is broken and instead of fixing it in version with a minor release, I added additional features and redesigned part of the architecture and moved to `v3.0.0`

---

## Quick links

**Repo home (README, Dockerfile, compose):** [see repository root](https://github.com/JJWren/FairShare).

**Releases (v4.0.0):** [see GitHub Releases on the sidebar](https://github.com/JJWren/FairShare/releases).

**Live demo / login:** see demo site (with demo credentials on the page). 
https://fairshare.theguywiththedogs.dev
