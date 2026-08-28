# FairShare.Api

ASP.NET Core Web API — auth, persistence, and the HTTP surface for everything in [`docs/API.md`](../../docs/API.md).

## Responsibilities

- **Auth**: ASP.NET Identity (SQLite via EF Core) + JWT bearer access tokens and single-use rotating refresh tokens (`Auth/TokenService.cs`). Login lockout, uniform 401s; public sign-up is Google OAuth only ([ADR 0004](../../docs/adr/0004-external-oauth-only-public-accounts.md)) — there is no self-registration endpoint.
- **Controllers** (`Controllers/`): `AuthController` (login/refresh/guest/change-password), `UsersController` (admin CRUD + password reset), `ParentsController` (ownership-scoped profiles with optimistic concurrency), `CatalogController` + `CalculationsController` (thin HTTP wrappers over `FairShare.Domain`).
- **Startup** (`Program.cs`): single-file minimal hosting — Identity/JWT config, rate limiting (per-IP, strict policy on the auth endpoints, `/healthz` exempt), CORS pinned to configured origins, forwarded-proto handling for reverse proxies, and the auto-migrate block (SQLite integrity check + pre-migration zip backup before applying migrations).
- **Background work** (`Services/`): `AdminSeeder` (first-boot admin account) and `RefreshTokenCleanupService` (purges expired/stale-revoked refresh tokens every 6 h).

## Key conventions

- Every data query is scoped to the authenticated user's id — never trust a record id alone.
- Failures return RFC 7807 problem details; validation errors are grouped by Identity error code.
- Config comes from `appsettings.json` < `appsettings.{Env}.json` < environment variables (`Section__Key`). Docker maps `.env` values in `docker-compose.yml`. See the configuration table in the root README.
- Swagger UI is registered only in Development. Don't move it out of that block for a public deployment.
- New schema changes = a new EF migration in `Migrations/`; the startup block applies them (backup first) when `AutoMigrate` is true.

## Worksheet export (`Services/Export`)

`POST .../calculations/export/xlsx` fills the **official AOC workbook** for the form and returns it. `WorksheetTemplates` describes each embedded workbook (`Templates/{state}/{FORM}.xlsx`, embedded resource; sheet name; the cells for number of children, each parent's five inputs, the two caption-line names; and every worksheet line's cells, which the oracle test uses). `ClosedXmlWorksheetExporter` writes only those input cells, then saves with `EvaluateFormulasBeforeSaving` (the only way ClosedXML persists cached values - `RecalculateAllFormulas()` alone does not) and `FullCalculationOnLoad`, so values are present for viewers that trust cached values and Excel recalculates on open. The workbook's formulas and sheet protection are left exactly as published - the sheet proves its own numbers.

Adding a form's template: embed its workbook (file name without dashes so the manifest name is `FairShare.Api.Templates.{state}.{FORM}.xlsx`), add a `WorksheetTemplate` to `WorksheetTemplates.All`, and the endpoint, the API tests and `WorkbookOracleTests` pick it up.

## Run

```bash
dotnet run                    # https://localhost:7080, Swagger at /swagger
dotnet test ../FairShare.Tests # integration tests boot this project in-memory
```

Dev uses `appsettings.Development.json` (committed dev signing key, local `fairshare.db`) — safe to experiment, never reuse those values in production.
