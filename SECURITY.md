# Security Policy

If you believe you’ve found a security vulnerability in FairShare, please do **not** open a public issue.
Instead, email: theguywiththedogs.dev@gmail.com with details and a proof of concept if possible.

I will do my best to respond within 7 days.

## Baseline hardening

For context when assessing reports, a deployed instance already includes: JWT bearer
auth with short-lived access tokens; single-use rotating refresh tokens (hashed at
rest, HttpOnly cookie, revoked in bulk on password change/reset, stale rows purged
periodically); login lockout with uniform 401 responses; per-IP rate limiting with a
stricter budget on the auth endpoints; self-registration disabled by default; CORS
pinned to configured origins; and CSP/security headers on the web container.

Known accepted trade-off: outstanding access tokens (up to `Jwt:AccessTokenMinutes`,
default 30) remain valid after a password change or reset - JWTs are not
security-stamp-validated per request.
