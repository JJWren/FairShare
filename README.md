![CI](https://github.com/JJWren/FairShare/actions/workflows/ci.yml/badge.svg)

# FairShare

*Lightweight child-support “what-if” calculator (currently Alabama). Built with **Blazor WebAssembly (WASM)**, ASP.NET Core, and SQLite. Optimized for zero-latency browser-side calculations.*

FairShare gives a quick, transparent estimate of who pays child support and how much under Alabama’s guidelines (CS-42 / CS-42-S). By leveraging WebAssembly, calculations happen instantly in your browser without waiting for a server response.

> ⚠️ Disclaimer: Informational/educational only. Not legal advice. Not a substitute for an attorney or court-approved worksheets. 

**Live demo:** https://fairshare.theguywiththedogs.dev

Demo credentials are displayed on the login page: `demo` / `Demo@123456!` (*limited access*). A Guest entry link is also present.

---

## What’s new in v5

- **Architecture Overhaul**: Migrated from MVC to a decoupled **Blazor WebAssembly SPA**.
- **Instant Calculations**: The core calculator logic now runs directly in the browser's WASM runtime.
- **Pure Blazor Identity**: Modernized authentication flow with pure Blazor components and .NET Identity APIs.
- **Improved Performance**: Reduced server load and zero network latency during scenario modeling.
- **.NET 10 (Preview)**: Updated to the latest .NET stack for performance and security.

---

## Features

- **High Performance**: Interactive forms with real-time updates as you type.
- **State Support (Alabama)**:
  - CS-42 (Standard) calculations.
  - CS-42-S (Shared Parenting/SPCA) calculations.
- **Responsive UI**: Two-column layout optimized for both desktop and mobile (Bootstrap 5).
- **Theme Support**: Light/Dark/Auto theme toggle.
- **Data Persistence**: Save and manage Parent Profiles (Plaintiff vs Defendant).
- **Admin Tools**: Comprehensive user management and automated database seeding.
- **Health & Safety**: Integrated database integrity checks and automated backup zipping on startup.

---

## Roles & Permissions
| Role      | Typical Access                  | Notes                                                                                                                                  |
| --------- | ------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------- |
| **Guest** | Limited preview                 | Navigate basic UI; no saving or settings. (Entry link on login page.)                                                                  |
| **User**  | Normal app usage                | Create and run scenarios, save parent profiles.                                                                                        |
| **Admin** | Full administration             | Manage users, roles, and access the users dashboard.                                                                                   |

---

## Tech Stack

- **Frontend**: Blazor WebAssembly (.NET 10)
- **Backend**: ASP.NET Core Web API
- **Shared Logic**: .NET Class Library (Shared Models/Calculators)
- **Database**: SQLite (EF Core)
- **Styling**: Bootstrap 5 + Bootstrap Icons
- **Deployment**: Docker & Docker Compose

---

## Quick Start (Docker)

FairShare uses a multi-project Docker build. Use the following `docker-compose.yml` snippet:

```yaml
services:
  fairshare:
    image: ghcr.io/jjwren/fairshare:latest
    environment:
      ASPNETCORE_ENVIRONMENT: "Production"
      ASPNETCORE_HTTP_PORTS: "9090"
      UI__DefaultTheme: "dark"
      ConnectionStrings__Default: "Data Source=/data/fairshare.db"
      AdminSeed__Enabled: "true"
      AdminSeed__User: "${AdminSeed__User}"
      AdminSeed__Password: "${AdminSeed__Password}"
    ports:
      - "9090:9090"
    volumes:
      - "./data:/app/data"
    healthcheck:
      test: ["CMD", "curl", "-fsS", "http://localhost:9090/healthz"]
      interval: 10s
      timeout: 2s
      retries: 6
    restart: unless-stopped
```

---

## Configuration

| Variable                           | Default      | Purpose                                                                                |
| ---------------------------------- | ------------ | -------------------------------------------------------------------------------------- |
| `ASPNETCORE_HTTP_PORTS`            | `9090`       | Internal port inside the container.                                                    |
| `AdminSeed__Enabled`               | `true`       | Enables seeding the initial admin account.                                             |
| `AdminSeed__User`                  | `admin`      | Username for the initial admin.                                                        |
| `AdminSeed__Password`              | `random`     | Password for the initial admin (logged on first run if empty).                         |

---

## Contributing

Please ensure business logic is kept in the `FairShareShared` project to maintain compatibility between the WASM client and the API server.

---

## License

Apache 2.0. See `LICENSE`.

---

## Support

Issues → [GitHub Issues](https://github.com/JJWren/FairShare/issues).

### Enjoy my work?
[![Buy Me A Coffee](https://img.shields.io/badge/Buy%20me%20a%20coffee-%23FFDD00?logo=buy-me-a-coffee&logoColor=black&labelColor=%23FFDD00)](https://www.buymeacoffee.com/jmykitta)

