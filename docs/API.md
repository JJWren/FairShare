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
| GET | `/auth/config` | none | Server capabilities: `{ "allowSelfRegistration": bool }`. |
| POST | `/auth/register` | none | Create an account. Returns **403** when self-registration is disabled (the default). |
| POST | `/auth/login` | none | Exchange credentials for tokens. |
| POST | `/auth/guest` | none | Issue a guest session. Takes no request body; returns the standard token response below. |
| POST | `/auth/refresh` | refresh cookie | Rotate the refresh token, get a new access token. |
| POST | `/auth/logout` | refresh cookie | Revoke the presented refresh token, clear the cookie. 204. |
| POST | `/auth/change-password` | Bearer, NotGuest | Change your own password. Revokes **all** of your refresh tokens, then returns fresh ones so the current session survives. |

**Request bodies**

```json
// login / register
{ "userName": "alice", "password": "correct-horse-1" }

// change-password
{ "currentPassword": "old-1", "newPassword": "new-passw0rd", "confirmNewPassword": "new-passw0rd" }
```

**Token response** (login / register / guest / refresh / change-password):

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
| GET | `/states/{state}/forms` | Calculation forms for a state (e.g. `AL` → CS-42, CS-42-S). |

## Calculations — `/api/v1/states/{state}/forms/{form}/calculations` (Bearer; guests allowed)

| Method | Path | Description |
|---|---|---|
| POST | `/states/AL/forms/CS42/calculations` | Run a calculation. Unknown state/form pairs return 404. |

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

**Response**

```json
{
  "success": true,
  "errors": [],
  "state": "AL",
  "form": "CS42",
  "numberOfChildren": 2,
  "payer": "Defendant",
  "finalAmount": 812
}
```

`errors[]` entries carry `code`, `message`, optional `field`, and `severity` when validation of the inputs fails (`success: false`).

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

## Misc

| Method | Path | Auth | Description |
|---|---|---|---|
| GET | `/healthz` | none | Liveness probe: `{ "status": "ok" }`. Exempt from rate limiting. |

---

## Security behaviors to expect (summary)

These are features, not bugs, when testing:

- `POST /auth/register` → 403 unless the operator enabled self-registration.
- 11th auth request within a minute → 429.
- Reused refresh cookie → 401 (rotation detected).
- Guest token on any NotGuest/Admin endpoint → 403.
- Password change/reset kills every other session for that user (outstanding *access* tokens survive up to 30 minutes by design — they are not revocable).
- Production deployments serve no `/swagger`, no stack traces, and CORS only for the configured web origin.
