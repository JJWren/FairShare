# FairShare Setup Guide

The complete operator guide for deploying and running a FairShare instance. The [README's quick start](../README.md#quick-start-docker-compose) gets you a local instance with admin-created accounts only; this guide covers everything after that — the full configuration reference, Google sign-in, donations, public hosting, and hardening.

Contents:

1. [Deploying with Docker Compose](#1-deploying-with-docker-compose)
2. [Environment reference (`.env`)](#2-environment-reference-env)
3. [API configuration reference](#3-api-configuration-reference)
4. [Google sign-in](#4-google-sign-in)
5. [Donations](#5-donations)
6. [Hardening a public instance](#6-hardening-a-public-instance)
7. [Hosting behind a reverse proxy](#7-hosting-behind-a-reverse-proxy)
8. [Data, backups & retention](#8-data-backups--retention)
9. [Verifying a deployment](#9-verifying-a-deployment)

---

## 1. Deploying with Docker Compose

Requires Docker with Compose v2.

```bash
cp .env.example .env
# Fill in .env — see section 2. JWT_SIGNING_KEY is the only hard requirement.

docker compose up --build
```

- Web app: `http://localhost:${WEB_PORT}` (default 5858)
- API: `http://localhost:${API_PORT}` (default 5859), health at `/healthz`

Both images build from source — no registry needed. Swagger is Development-only and not served by the Production compose build.

**Prebuilt images:** every release publishes `ghcr.io/jjwren/fairshare-api:<version>` and `ghcr.io/jjwren/fairshare-web:<version>` (plus `:latest` = newest release), and every merge to `main` publishes `:main` (and `:sha-<short>`). To run a pinned version instead of building, replace each service's `build:` with `image: ghcr.io/jjwren/fairshare-api:<version>` and `image: ghcr.io/jjwren/fairshare-web:<version>`; the environment and volume settings are unchanged.

The API only reads its environment at startup — after changing `.env`, run `docker compose up -d` again.

## 2. Environment reference (`.env`)

Every variable in [`.env.example`](../.env.example), what it does, and — the part that bites — what happens when it is absent.

| Variable | Required | Default | Behavior |
|---|---|---|---|
| `JWT_SIGNING_KEY` | **Yes** | — | HMAC-SHA256 key signing access tokens; 32+ random bytes (`openssl rand -base64 48`). The API will not run meaningfully without it. Rotating it invalidates outstanding access tokens (≤30 min); sessions recover silently via the refresh cookie. |
| `ADMIN_USER` | No | `admin` | Username of the admin account seeded on first run. |
| `ADMIN_PASSWORD` | No | *(random)* | Password for the seeded admin. Left empty, a generated one is printed once in `docker compose logs api` — treat it as burned (Docker persists logs) and change it after first login. |
| `ADMIN_SEED_ENABLED` | No | `true` | Set to `false` after the first successful boot — the seeder only matters once. |
| `ADMIN_SEED_LOG_GENERATED_PASSWORD` | No | `true` | Whether a generated admin password is printed to the api container log. |
| `GOOGLE_CLIENT_ID` / `GOOGLE_CLIENT_SECRET` | No | *(empty)* | Google OAuth client for public sign-in (section 4). **Absent, the deployment has no public sign-up path at all**: the Google button is hidden, `/api/v1/auth/google/start` returns 404, and accounts exist only if an admin creates them. |
| `DONATE_URL` | No | *(empty)* | The `https` URL of your Buy Me a Coffee (or similar) page (section 5). Absent (or not an absolute `https` URL), the donate button is hidden and `/go/donate` returns 404; the `/support` page still explains the project. |
| `WEB_PORT` / `API_PORT` | No | `5858` / `5859` | Host ports (browser-visible). |
| `WEB_ORIGIN` | No | `http://localhost:5858` | The web app's browser-visible URL — used for CORS. Must match what users actually type. |
| `API_BASE_URL` | No | `http://localhost:5859` | The API's **browser-visible** URL — never the compose-internal service name. |

## 3. API configuration reference

Settings read by `FairShare.Api` (compose maps the `.env` variables above onto these; under bare `dotnet run` set them via `appsettings.json`, user-secrets, or environment variables).

| Setting (API) | Default | Purpose |
| --- | --- | --- |
| `ConnectionStrings:Default` | — | SQLite connection string. |
| `Jwt:SigningKey` | — | HMAC-SHA256 signing key for access tokens. Required; set via user-secrets/env var in real deployments. |
| `Jwt:AccessTokenMinutes` | `30` | Access token lifetime. |
| `Jwt:RefreshTokenDays` | `30` | Refresh token lifetime. |
| `Cors:AllowedOrigins` | `[]` | Origins allowed to call the API (the Web app's URL). |
| `Authentication:Google:ClientId` / `Authentication:Google:ClientSecret` | *(unset)* | The Google OAuth client (section 4). Both set = Google sign-in on; either absent = no public sign-up path. |
| `Donations:BuyMeACoffeeUrl` | *(unset)* | Absolute `https` URL behind `/go/donate` (section 5); anything else disables the donations surface. |
| `AdminSeed:Enabled` | `true` | Enables seeding the initial admin account. Disable after first boot. |
| `AdminSeed:User` | `admin` | Username for the initial admin. |
| `AdminSeed:Password` | *(random)* | Password for the initial admin (logged on first run if empty). |
| `AdminSeed:LogGeneratedPassword` | `true` | Whether a generated admin password is printed to the log. |
| `RateLimiting:Enabled` | `true` | Kill-switch for rate limiting (values are fixed: 100 req/min per IP globally, 10 req/min per IP on the auth endpoints). |
| `DataProtection:KeysPath` | *(unset)* | Directory for ASP.NET DataProtection keys (Identity tokens). Set it to a volume path (the compose stacks use `/data/keys`) so keys survive container recreates; unset = framework default inside the container. |
| `HttpsRedirection:HttpsPort` / `HTTPS_PORT` / `ASPNETCORE_HTTPS_PORT` | *(unset)* | Only needed when Kestrel itself serves TLS (any of the three keys, a valid port number). Behind a TLS-terminating proxy leave it unset: the API then skips `UseHttpsRedirection` (the proxy forces HTTPS) instead of logging "Failed to determine the https port for redirect" on every start. |

| Setting (Web) | Default | Purpose |
| --- | --- | --- |
| `Api:BaseUrl` | — | Base URL of `FairShare.Api` to call. |

## 4. Google sign-in

Google OAuth is the **only public sign-up path** (see `docs/adr/0004`): without it, accounts are admin-created only. First Google sign-in creates the account; FairShare stores only the Google subject ID and email, never a password.

The guided way: run the wizard, which walks you through the Google Cloud console and writes the values into your stack's `.env`:

```bash
scripts/setup-google-signin.sh
```

What it does (and what to do manually if you prefer):

1. **Create a Google Cloud project** (free) and an **OAuth consent screen**: user type *External*, app name **FairShare**, your email as support/developer contact, your site's domain as an authorized domain, no extra scopes. **Publish** the app (move it out of "Testing" — in Testing only allow-listed accounts can sign in).
2. **Create an OAuth client**: *Create credentials → OAuth client ID*, application type *Web application*. The single authorized redirect URI is exactly `{API origin}/signin-google` (e.g. `https://api.example.com/signin-google`). No JavaScript origins are needed — the flow is server-driven.
3. Put the **Client ID** and **Client secret** into `.env` (`GOOGLE_CLIENT_ID` / `GOOGLE_CLIENT_SECRET`) and restart the stack.

### Passing Google's app verification

Plain email-scope sign-in works unverified, but if you request verification (e.g. to clear the consent-screen warning), Google's reviewers check the app's public face. Lessons from a real verification attempt:

- **Verify domain ownership first**: the home page URL's domain must be verified in [Google Search Console](https://search.google.com/search-console) **with the same Google account that owns the Cloud project**, before the verification request.
- **The home page must name the app and state its purpose**: the consent screen's app name has to match the name shown on the home page, and the page must say what the app does. FairShare's landing page does both (the `FairShare` heading and purpose line on the state picker) — keep them intact.
- **Fill in the consent screen's branding links**: app home page = your web origin; privacy policy = `{web origin}/privacy`; terms of service = `{web origin}/terms`. Both pages ship with the app.

## 5. Donations

Optional, and off by default. Set `DONATE_URL` to the absolute `https` URL of your Buy Me a Coffee (or similar) page and restart. With it set, the `/support` page shows a donate button that routes through the API's first-party `/go/donate` redirect — so the click is counted as an anonymous `donate-click` event (rule-filtered: bots, Do Not Track/Global Privacy Control, and admins are never counted) and the destination stays operator-controlled. Anything other than an absolute `https` URL disables the whole surface (`/go/donate` returns 404).

## 6. Hardening a public instance

- **Public sign-up is Google-only**: there is no username/password registration to abuse. The local (username/password) sign-in exists for admin and family accounts created from **Admin → Users**.
- **Admin bootstrap:** set a strong `ADMIN_PASSWORD` in `.env` before first boot. If you let the seeder generate one, treat it as burned — Docker persists container logs — so log in, change it from the **Account** page, and consider renaming the account (`ADMIN_USER`); every credential-stuffing bot tries `admin` first.
- **Admin two-factor:** admin accounts require a TOTP authenticator code at sign-in; enroll from the **Account** page.
- **After first boot**, set `ADMIN_SEED_ENABLED=false` and remove `ADMIN_PASSWORD` from `.env` — the seeder only matters once.
- **Signing key:** generate a fresh `JWT_SIGNING_KEY` for production (`openssl rand -base64 48`); never reuse a key that has been committed anywhere. Rotating it only invalidates outstanding access tokens (≤30 min); sessions recover silently via the refresh cookie.
- **Passwords:** users change their own via **Account → Change password**; admins reset others' via **Admin → Users → Edit**. Both revoke all of that user's refresh tokens.

## 7. Hosting behind a reverse proxy

Terminate TLS at your proxy and forward `X-Forwarded-Proto` (the API honors it for cookie security attributes). Set `WEB_ORIGIN` to the web app's public URL (CORS) and `API_BASE_URL` to the API's public URL — `API_BASE_URL` must always be the *browser-visible* API URL, never the compose-internal service name.

Rate limiting keys on the direct peer IP: behind a reverse proxy every client collapses into the proxy's bucket. That is deliberate — trusting `X-Forwarded-For` without pinning the proxy in `KnownProxies` would let clients spoof their way out of throttling — so pin your proxy before switching the limiter to forwarded addresses.

Leave the `HttpsRedirection` keys unset when the proxy terminates TLS (section 3).

## 8. Data, backups & retention

- The SQLite database lives in the named `fairshare-data` volume; it also holds pre-migration backups (zipped automatically on startup) and, when `DataProtection:KeysPath` points there (`/data/keys` in the shipped compose), the DataProtection keys.
- Retention is automatic (see `docs/adr/0003`): raw analytics rows roll up nightly and are deleted after **90 days** (daily aggregates kept), diagnostic logs after **30 days**, audit events after about **one year**. Deleting an account removes everything it owns immediately; only its audit events survive until their own expiry.
- Back up the volume itself (e.g. `docker run --rm -v fairshare-data:/data -v "$PWD":/backup alpine tar czf /backup/fairshare-data.tgz /data`) with the stack stopped, or rely on the startup backup zips for point-in-time copies.

## 9. Verifying a deployment

- `GET {API origin}/healthz` → `200`.
- `GET {API origin}/api/v1/auth/config` → `{"googleEnabled":true,"donationsEnabled":true}` — each `false` means the corresponding `.env` values are absent or invalid (sections 4–5).
- Load the web origin: the state picker renders; the footer links Terms, Privacy, and Support pages.
- Sign in with a Google account, then check **Admin → Stats** (as admin) for the first counted events.
