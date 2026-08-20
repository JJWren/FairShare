# FairShare API Reference

REST API for the FairShare child-support calculator. JSON everywhere, JWT bearer auth, versioned under `/api/v1`.

All examples use the local dev URL (`http://localhost:5080`). Substitute your own deployment's API origin. Interactive Swagger UI is available at `/swagger` **in Development only** — it is deliberately disabled in Production builds.

## Authentication model

| Piece | Details |
|---|---|
| Access token | JWT (HMAC-SHA256), returned in auth response bodies. Send as `Authorization: Bearer <token>`. Lifetime: 30 min (`Jwt:AccessTokenMinutes`). |
| Refresh token | Opaque value in an `HttpOnly` cookie (`fairshare_refresh`, `Path=/api/v1/auth`). **Single-use**: every call to `/auth/refresh` revokes the presented token and issues a new one. Replaying a consumed token returns 401. Lifetime: 30 days (`Jwt:RefreshTokenDays`). Stored server-side as a SHA-256 hash. |
| Guest | `POST /auth/guest` issues a token with a `guest` claim — can run calculations and browse the catalog, cannot save data or manage anything. |
| Roles | `User` (default), `Admin` (user management). Endpoints marked **Admin** require the Admin role; endpoints marked **NotGuest** reject guest tokens with 403. |

A typical non-browser client only needs the access token: log in, use the bearer, log in again when it expires. The refresh cookie exists primarily for the SPA.

### Auth session flow

```
POST /api/v1/auth/login ─────► 200 { accessToken, ... } + Set-Cookie: fairshare_refresh
        │
        ▼ (access token expires after 30 min)
POST /api/v1/auth/refresh (cookie) ─► 200 { new accessToken } + rotated cookie
        │
        ▼
POST /api/v1/auth/logout (cookie) ──► 204, cookie consumed + cleared
```

## Conventions

- **Errors** are RFC 7807 problem details (`application/problem+json`). Validation failures carry an `errors` object keyed by error code:
  ```json
  { "title": "One or more validation errors occurred.", "status": 400,
    "errors": { "PasswordTooShort": ["Passwords must be at least 8 characters."] } }
  ```
- **Login failures are uniform**: wrong password, unknown user, disabled account, and lockout all return a bare `401` with no body — deliberately indistinguishable.
- **Lockout**: 5 consecutive failed logins lock the account for ~5 minutes.
- **Rate limits** (per client IP): 100 requests/min globally; **10 requests/min shared across** `login`, `register`, `guest`, and `refresh`. Exceeding either returns `429` with `Retry-After: 60`. `/healthz` is exempt.

---

## Auth — `/api/v1/auth`

| Method | Path | Auth | Description |
|---|---|---|---|
| GET | `/auth/config` | none | Server capabilities: `{ "googleEnabled": bool }`. |
| GET | `/auth/google/start?returnUrl=&remember=` | none | Begins the Google sign-in flow (top-level navigation, not XHR). **404** when Google is not configured. The only public sign-up path (ADR 0004) — `/auth/register` no longer exists. |
| GET | `/auth/google/complete` | (flow-internal) | Lands the Google result: finds or creates the account (storing only the Google subject ID and email), sets the refresh cookie, redirects back to the SPA. |
| POST | `/auth/login` | none | Exchange local credentials for tokens. Accounts with TOTP enabled get `401 { "requiresTwoFactor": true }` until a valid `twoFactorCode` accompanies the password — the challenge appears only *after* the password verified. |
| POST | `/auth/guest` | none | Issue a guest session. Takes no request body; returns the standard token response below. |
| POST | `/auth/refresh` | refresh cookie | Rotate the refresh token, get a new access token. Preserves the session's remember-this-device choice. |
| POST | `/auth/logout` | refresh cookie | Revoke the presented refresh token, clear the cookie. 204. |
| POST | `/auth/change-password` | Bearer, NotGuest | Change your own password. Revokes **all** of your refresh tokens, then returns fresh ones so the current session survives. |
| POST | `/auth/account/username` | Bearer, NotGuest | Change your display name: `{ "newUserName" }` → fresh token response (the name rides in the JWT). |
| DELETE | `/auth/account` | Bearer, NotGuest | **Hard delete**: `{ "confirm": "DELETE" }` → profiles, sessions, external logins, and the account are gone at once (204). Audit rows naming the account survive until their ~1-year retention expires. |
| GET | `/auth/2fa/status` | Bearer, Admin | `{ "enabled": bool }`. TOTP is scoped to local Admin accounts; Google users inherit Google's 2FA. |
| GET | `/auth/2fa/setup` | Bearer, Admin | `{ "sharedKey", "authenticatorUri" }` for authenticator-app enrollment. |
| POST | `/auth/2fa/enable` / `/auth/2fa/disable` | Bearer, Admin | `{ "code": "123456" }` → 204; 400 when the code does not match. |

**Remember this device**: `login`'s `rememberDevice` (and `google/start`'s `remember`) — `false` (default) issues a session cookie backed by a 1-day server row; `true` keeps the 30-day rotating cookie.

**Request bodies**

```json
// login
{ "userName": "alice", "password": "correct-horse-1", "twoFactorCode": null, "rememberDevice": false }

// change-password
{ "currentPassword": "old-1", "newPassword": "new-passw0rd", "confirmNewPassword": "new-passw0rd" }
```

**Token response** (login / guest / refresh / change-password / account/username):

```json
{
  "accessToken": "eyJhbGciOi...",
  "accessTokenExpiresUtc": "2026-01-01T12:34:56Z",
  "userName": "alice",
  "role": "User",
  "isGuest": false
}
```

Password rules: minimum 8 characters, at least one digit and one lowercase letter.

---

## Catalog — `/api/v1/states` (Bearer; guests allowed)

| Method | Path | Description |
|---|---|---|
| GET | `/states` | Supported states. |
| GET | `/states/{state}/forms` | Calculation forms for a state (e.g. `AL` → CS-42, CS-42-S). Each entry carries `form` (route key), `displayName` (with revision, e.g. `CS-42 (Rev. 5/2022)`), `description`, and `isSharedCustody`. |

## Calculations — `/api/v1/states/{state}/forms/{form}/calculations` (Bearer; guests allowed)

| Method | Path | Description |
|---|---|---|
| POST | `/states/AL/forms/CS42/calculations` | Run a calculation. Unknown state/form pairs return 404. |
| POST | `/states/AL/forms/CS42/calculations/export/xlsx` | Download the **official AOC workbook** for the form with these figures typed into its input cells. Body = the calculation request plus optional `plaintiffName` / `defendantName` (max 100 chars) for the caption line. Returns `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet` with `Content-Disposition: attachment; filename=FairShare_AL_CS-42_{yyyyMMdd}.xlsx`. Only input cells are written - the workbook's formulas stay live and it recalculates on open. The calculation is run first: if it does not succeed (e.g. `INCOME_ABOVE_SCHEDULE`, `INVALID_CHILD_COUNT`) the endpoint returns **400** with the usual `CalculationResponse` body instead of a workbook full of `#N/A`. 404 when no template is registered for the state/form. |

**Request**

```json
{
  "numberOfChildren": 2,
  "plaintiff": {
    "hasPrimaryCustody": true,
    "monthlyGrossIncome": 4200,
    "preexistingChildSupport": 0,
    "preexistingAlimony": 0,
    "workRelatedChildcareCosts": 400,
    "healthcareCoverageCosts": 250
  },
  "defendant": {
    "hasPrimaryCustody": false,
    "monthlyGrossIncome": 5100,
    "preexistingChildSupport": 0,
    "preexistingAlimony": 0,
    "workRelatedChildcareCosts": 0,
    "healthcareCoverageCosts": 0
  }
}
```

**Response** (trimmed — `lines[]` carries every numbered line of the worksheet)

```json
{
  "success": true,
  "errors": [],
  "state": "AL",
  "form": "CS42",
  "numberOfChildren": 2,
  "payer": "Defendant",
  "finalAmount": 1253,
  "lines": [
    { "number": "1",  "label": "MONTHLY GROSS INCOME",            "plaintiff": 4200, "defendant": 5100, "combined": 9300, "format": "Currency" },
    { "number": "3",  "label": "PERCENTAGE SHARE OF INCOME",      "plaintiff": 0.45, "defendant": 0.55, "combined": 1.00, "format": "Percent" },
    { "number": "4",  "label": "BASIC CHILD SUPPORT OBLIGATION",  "plaintiff": null, "defendant": null, "combined": 1629, "format": "Currency" },
    { "number": "13", "label": "RECOMMENDED CHILD-SUPPORT ORDER", "plaintiff": 376,  "defendant": 1253, "combined": null, "format": "Currency" }
  ]
}
```

The calculators mirror the official AOC workbooks line by line (see `docs/adr/0001-mirror-official-worksheet-lines.md`): `lines[]` is the worksheet in form order, a `null` column means the paper form has no cell there, and `format` is `Currency` (whole dollars) or `Percent` (a fraction, `0.45` = 45%). On CS-42 the order applies to the parent without `hasPrimaryCustody`; on CS-42-S (which ignores the flag) it is the higher line-13 amount, and a tie comes back as `payer: ""` / `finalAmount: 0` ("no net transfer").

`errors[]` entries carry `code`, `message`, optional `field`, and `severity` when validation of the inputs fails (`success: false`, `lines: []`). Codes: `INVALID_CHILD_COUNT` (children outside 1–6), `INCOME_ABOVE_SCHEDULE` (combined adjusted gross income rounds above the $30,000 top of the schedule — the guidelines leave that to the court), `UNEXPECTED_ERROR`.

---

## Parents — `/api/v1/parents` (Bearer)

Saved parent profiles. **Ownership-scoped**: every query filters by the authenticated user; you can never read or modify another user's records.

| Method | Path | Auth | Description |
|---|---|---|---|
| GET | `/parents` | Bearer | List your saved parents. |
| GET | `/parents/{id}` | Bearer | Fetch one (404 if not yours). |
| POST | `/parents` | NotGuest | Create. `displayName` is the natural key — re-saving an existing name updates that record in place. |
| PUT | `/parents/{id}` | NotGuest | Update. Optimistic concurrency: echo the `rowVersion` from a prior GET; a stale value returns **409 Conflict**. |
| POST | `/parents/{id}/archive` | NotGuest | Archive (soft delete). |

**Create/Update body**

```json
{
  "displayName": "Jane D",
  "monthlyGrossIncome": 4000,
  "preexistingChildSupport": 0,
  "preexistingAlimony": 0,
  "workRelatedChildcareCosts": 300,
  "healthcareCoverageCosts": 150,
  "hasPrimaryCustody": true,
  "rowVersion": "AAAAAAAAB9E="   // PUT only, optional but recommended
}
```

---

## Admin — `/api/v1/admin/users` (Bearer + Admin role)

| Method | Path | Description |
|---|---|---|
| GET | `/admin/users?filter=all\|enabled\|disabled` | List users. |
| GET | `/admin/users/{id}` | Fetch one user. |
| POST | `/admin/users` | Create: `{ "userName", "password", "confirmPassword", "role": "User"\|"Admin" }`. |
| PUT | `/admin/users/{id}` | Update: `{ "id", "userName", "role", "isDisabled" }`. Disabling a user kills their refresh path immediately. |
| POST | `/admin/users/{id}/reset-password` | `{ "newPassword", "confirmNewPassword" }` → 204. Clears any lockout and revokes all of the user's refresh tokens. |
| DELETE | `/admin/users/{id}` | Delete. Self-delete is rejected (400). |

---

## Analytics beacon — `/api/v1/analytics` (anonymous)

First-party, cookieless capture endpoints for the SPA (see `docs/adr/0003-first-party-cookieless-analytics.md`). Both return **204 regardless of whether anything was recorded** — the server applies every rule (DNT/`Sec-GPC` opt-out, bot user-agents, excluded paths, admin's own browsing) and callers cannot observe the outcome. No cookies, no identifiers: visitors are counted by a daily-rotating HMAC that cannot link two days together.

| Method | Path | Description |
|---|---|---|
| POST | `/analytics/page-views` | `{ "path": "/states/al/cs42", "referrer": "https://..." }`. `referrer` only on the SPA's first load. |
| POST | `/analytics/events` | `{ "name": "gated-hit", "target": "profiles" }`. Only whitelisted client event names are accepted (`gated-hit`); server-observed events (calculations) are recorded server-side and cannot be posted. Targets are restricted to short kebab-case tokens. |

## Admin stats — `/api/v1/admin/stats` (Bearer + Admin role)

`days` selects the period ending today (`7`, `30`, ...); omit or `0` for all time. Completed days come from nightly rollups; today is computed live.

| Method | Path | Description |
|---|---|---|
| GET | `/admin/stats/summary?days=30` | Tiles: `{ pageViews, dailyVisitors, calculationsCompleted, gatedHits, donateClicks, firstDay }`. |
| GET | `/admin/stats/pages?days=&page=&pageSize=&sort=views\|visitors\|path&desc=` | Top pages, paged: `{ items: [{ path, views, visitors }], page, pageSize, totalCount }`. |
| GET | `/admin/stats/referrers?days=` | Top 10 external referrer hosts: `[{ referrerHost, views }]`. |
| GET | `/admin/stats/events?days=` | Event counts: `[{ name, target, count }]`, descending. |

## Admin logs — `/api/v1/admin/logs` (Bearer + Admin role)

Diagnostic logs persist 30 days; audit events ~1 year (and survive account deletion). Verbose mode raises capture to Debug and **always turns itself back off** (~4 h or process restart); toggling it is itself an audit event.

| Method | Path | Description |
|---|---|---|
| GET | `/admin/logs?level=&search=&page=&pageSize=` | Diagnostic logs, newest first. `level` = minimum (`Debug`/`Information`/`Warning`/`Error`); `search` matches message and category. |
| GET | `/admin/logs/audit?page=&pageSize=` | Audit events, newest first: `{ items: [{ occurredAtUtc, actorName, action, target, detail }], ... }`. |
| GET | `/admin/logs/verbose` | `{ "enabled": bool, "untilUtc": "..." }`. |
| PUT | `/admin/logs/verbose` | `{ "enabled": true\|false }` → the new status. |

---

## Misc

| Method | Path | Auth | Description |
|---|---|---|---|
| GET | `/go/donate` | none | First-party donate redirect: 302 to the operator's configured Buy Me a Coffee page (`Donations:BuyMeACoffeeUrl`, https only), counting an anonymous `donate-click` event first (same opt-out/bot/admin rules as all analytics). **404** when unconfigured. `GET /auth/config` exposes `donationsEnabled`. |
| GET | `/healthz` | none | Liveness probe: `{ "status": "ok" }`. Exempt from rate limiting. |

---

## Security behaviors to expect (summary)

These are features, not bugs, when testing:

- `POST /auth/register` → 404: self-registration is retired; Google sign-in is the only public sign-up path (and 404s when unconfigured).
- A wrong password never reveals whether the account has 2FA — the challenge body only follows a verified password.
- 11th auth request within a minute → 429.
- Reused refresh cookie → 401 (rotation detected).
- Guest token on any NotGuest/Admin endpoint → 403.
- Password change/reset kills every other session for that user (outstanding *access* tokens survive up to 30 minutes by design — they are not revocable).
- Production deployments serve no `/swagger`, no stack traces, and CORS only for the configured web origin.
