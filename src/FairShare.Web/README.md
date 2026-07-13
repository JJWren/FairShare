# FairShare.Web

Standalone Blazor WebAssembly SPA. Compiled to static WASM assets and served by nginx (Docker) or Kestrel (`dotnet run`); all data comes from `FairShare.Api` over HTTP/JSON — the browser calls the API directly, cross-origin.

## Layout

- `Auth/` — the client-side auth machinery:
  - `AuthApiClient` — typed wrapper for the `/api/v1/auth/*` endpoints; caches the server's auth-config flags (fail-closed).
  - `AuthTokenHandler` — `DelegatingHandler` that attaches the bearer token and, on 401, performs one serialized silent refresh + retry (skipped for anonymous auth endpoints so a failed login can't "succeed" via refresh).
  - `InMemoryTokenStore` — the access token lives in memory only (XSS hardening); page reloads re-hydrate via the HttpOnly refresh cookie (`Program.cs` calls `TryRefreshAsync()` before first render).
  - `JwtAuthenticationStateProvider` / `JwtParser` — claims → `AuthenticationState`; policies `AdminOnly` and `NotGuest` mirror the API's.
- `Pages/` — `Calculator`, `StateForms`, `Profiles` (saved parents), `Login`/`Register` (registration UI hides itself when the server reports self-registration disabled), `ChangePassword`, `Admin/*` (user management).
- `Components/`, `Layout/` — Bootstrap 5 UI; `ThemeToggle` persists light/dark/auto in `localStorage`, applied pre-render by `wwwroot/js/theme-init.js` (a separate file, not an inline script, so the CSP can stay `script-src 'self'`).
- `nginx.conf` + `fairshare-security-headers.inc` — production web server config: SPA fallback, cache policy, and CSP/security headers (the `.inc` is re-included in every location that sets its own `add_header`; nginx header inheritance is all-or-nothing).
- `docker-entrypoint.d/` — container-start scripts: `40-…` writes `Api:BaseUrl` from `API_BASE_URL`, `50-…` injects the API origin into the CSP `connect-src`. Both make the image deploy-agnostic without rebuilds.

## Runtime config

The SPA reads exactly one setting: `Api:BaseUrl` (`wwwroot/appsettings.json`, overwritten at container start). It must be the **browser-visible** API URL. Web and API must be served over the same scheme (both HTTPS or both HTTP) or browsers block the calls as mixed content.

## Run

```bash
dotnet run   # https://localhost:7090, expects the API on localhost:7080
```
