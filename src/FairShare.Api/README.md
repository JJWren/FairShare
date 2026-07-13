# FairShare.Api

ASP.NET Core Web API — auth, persistence, and the HTTP surface for everything in [`docs/API.md`](../../docs/API.md).

## Responsibilities

- **Auth**: ASP.NET Identity (SQLite via EF Core) + JWT bearer access tokens and single-use rotating refresh tokens (`Auth/TokenService.cs`). Login lockout, uniform 401s, registration gate (`Auth:AllowSelfRegistration`, default off).
- **Controllers** (`Controllers/`): `AuthController` (login/refresh/guest/change-password), `UsersController` (admin CRUD + password reset), `ParentsController` (ownership-scoped profiles with optimistic concurrency), `CatalogController` + `CalculationsController` (thin HTTP wrappers over `FairShare.Domain`).
- **Startup** (`Program.cs`): single-file minimal hosting — Identity/JWT config, rate limiting (per-IP, strict policy on the auth endpoints, `/healthz` exempt), CORS pinned to configured origins, forwarded-proto handling for reverse proxies, and the auto-migrate block (SQLite integrity check + pre-migration zip backup before applying migrations).
- **Background work** (`Services/`): `AdminSeeder` (first-boot admin account) and `RefreshTokenCleanupService` (purges expired/stale-revoked refresh tokens every 6 h).

## Key conventions

- Every data query is scoped to the authenticated user's id — never trust a record id alone.
- Failures return RFC 7807 problem details; validation errors are grouped by Identity error code.
- Config comes from `appsettings.json` < `appsettings.{Env}.json` < environment variables (`Section__Key`). Docker maps `.env` values in `docker-compose.yml`. See the configuration table in the root README.
- Swagger UI is registered only in Development. Don't move it out of that block for a public deployment.
- New schema changes = a new EF migration in `Migrations/`; the startup block applies them (backup first) when `AutoMigrate` is true.

## Run

```bash
dotnet run                    # https://localhost:7080, Swagger at /swagger
dotnet test ../FairShare.Tests # integration tests boot this project in-memory
```

Dev uses `appsettings.Development.json` (committed dev signing key, local `fairshare.db`) — safe to experiment, never reuse those values in production.
