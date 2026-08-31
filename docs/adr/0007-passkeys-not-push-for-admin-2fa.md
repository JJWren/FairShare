# ADR 0007 — Passkeys, not push notifications, for admin 2FA

**Status:** Accepted — 2026-08-31

## Context

Admin TOTP (ADR 0004, decision 7) works but is a chore: finding FairShare among many entries in an authenticator app, then typing a six-digit code. The wish was the Microsoft Authenticator experience — a notification arrives, the admin taps Approve or Deny. But no protocol exists for pushing to a third-party TOTP app: an `otpauth://` enrollment can only generate codes. True push would mean building FairShare's first outbound channel of any kind (the codebase deliberately has none — no email, no SMS, no push) or adopting a vendor. The audience is a handful of local Admin accounts; public users inherit Google's 2FA and are untouched.

## Decision

Admins get **WebAuthn passkeys** as a one-tap factor; the push idea is dropped. TOTP remains mandatory-enrolled and is always the fallback — a passkey is a convenience on top, never a replacement, so no lockout path exists.

1. **Registration is enablement.** An admin with at least one registered passkey gets the passkey prompt; there is no separate setting. Multiple named passkeys per admin, listed/renamed/revoked on the Account page; removing the last one is allowed because TOTP remains.
2. **Two sign-in shapes.** After a password, the passkey prompt replaces typing a code; and a "sign in with a passkey" button signs in usernameless via discoverable credentials (possession + user verification = both factors, so user verification is required there; after a password it is merely preferred).
3. **Step-up challenges ship in the same deploy.** Account-takeover-capable actions — change password, username change, account delete, 2FA enable/disable/setup (setup also becomes a POST; today a GET can reset the authenticator key), and all admin user-management mutations — demand a fresh second-factor confirmation. One coherent rule: any successful verification (sign-in or step-up, passkey or TOTP) starts a 5-minute elevation window. Non-admin accounts are unchanged; step-up applies only to accounts that have a second factor.
4. **"Remember this device" is untouched** — it stays a refresh-cookie-lifetime feature (ADR 0004, decision 4) and never skips the second factor.
5. **No attestation or authenticator-type policy** — any platform or roaming authenticator is acceptable for a handful of trusted admins.

## Alternatives considered

- **Self-hosted Web Push (PWA + service worker + VAPID + device subscriptions + pending-login approve/deny).** The literal reading of the wish, and the only self-hosted way a notification reaches a *different* device than the one logging in. Rejected: the largest build for the fewest users, FairShare's first outbound channel built as a side effect, unreliable on iOS browsers, and it imports push-fatigue attacks that passkeys are immune to.
- **A push-MFA vendor (e.g. Duo, free ≤10 users).** Smallest build for true remote push. Rejected: a third-party dependency in a deliberately self-hosted, privacy-first codebase, and admins would have to switch authenticator apps anyway.
- **Keep TOTP and just fix the ergonomics.** Honest but doesn't deliver one-tap; kept anyway as the permanent fallback.
