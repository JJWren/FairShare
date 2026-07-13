# FairShare.Tests

xUnit test project covering both layers:

- `Domain/` — pure unit tests for the calculators (`CS42CalculatorTests`, `CS42SCalculatorTests`). No host, no mocks — calculators are deterministic functions.
- `Api/` — end-to-end integration tests that boot the real API in-memory via `WebApplicationFactory<Program>` against a throwaway SQLite database.

## The API test harness

`FairShareApiFactory` is the shared fixture. Notable design points (read its comments before changing it):

- **Config via process environment variables**, not `ConfigureAppConfiguration` — env vars outrank `appsettings.Development.json` in host config precedence, which is the only way overrides reliably win with minimal-hosting apps. Originals are restored on dispose.
- Each fixture gets a **unique temp SQLite file**, deleted on dispose (after clearing the connection pool).
- **Rate limiting is disabled** in the base factory (`RateLimiting__Enabled=false`) so functional tests can hammer auth endpoints; `RateLimitingTests` re-enables it via a factory subclass to test the 429 path itself.
- All API test classes share the `[Collection("Api")]` collection so they run **sequentially** — parallel in-process test hosts flake on shared native SQLite init, and the env-var mechanism is process-wide.
- Test clients use an `https://localhost` base address so the Secure refresh cookie round-trips; the `HandleCookies = false` pattern (see `AuthEndpointsTests`) is used whenever a test needs to replay a specific stale cookie instead of letting the cookie jar rotate it away.

## Adding tests

- New feature with config? Subclass `FairShareApiFactory`, call `base.ConfigureWebHost(builder)`, then `SetEnvVar(...)` your overrides (see `RegistrationEnabledApiFactory`).
- Self-registration is disabled by default, so create extra users through the admin API (login as the seeded admin `admin` / `Adm!n-Test-12345` — test-fixture values only), not `/auth/register`.

```bash
dotnet test FairShare.sln
```
