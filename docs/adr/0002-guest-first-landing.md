# ADR 0002 — Guest-first landing

**Status:** Accepted — 2026-08-18

## Context

FairShare has a full Identity stack — accounts, roles, an admin console — but accounts are admin-provisioned. On a publicly hosted instance nearly every visitor is, and will remain, a guest. The app nevertheless landed everyone on the login screen: an anonymous visitor hitting `/` failed `[Authorize]`, was redirected to `/login`, and had to find the "Continue as Guest" link to reach the calculator.

That wall gated nothing. Guest access was one click away for anyone, so the login-first flow added friction without adding access control — and the majority audience (a parent who wants a CS-42 number) paid that friction on every visit.

## Decision

Guest is the **default identity**; the state picker is the effective landing page.

1. On startup, when re-hydrating the session from the refresh cookie fails, the Web app silently issues itself a guest session (`POST /auth/guest`) before first render. The login redirect remains only as a fallback for when the API is unreachable.
2. The login page is purely an **upgrade** to an account: "Continue as Guest" is removed (visitors already are guests) and replaced with a back link.
3. The navbar keeps the muted "Guest" badge (what you are) and adds a "Sign in" link with a `returnUrl` (what you can do about it).
4. Logging out of an account drops back to a fresh guest session on the state picker, not the login page.
5. `[Authorize]` stays on the public pages as defense in depth; the API remains the real gate (`NotGuest` / `AdminOnly` policies are unchanged).

Shipped as **3.0.0**: the default deployment posture changes from login-first to public-calculator, and a user-facing affordance ("Continue as Guest") is removed — operators deserve a major-version flag on that even though no API contract breaks.

## Consequences

- Every anonymous visit (crawlers included) mints a guest refresh-token row. Cost is bounded: the `auth` rate limiter caps the mint rate per IP and `RefreshTokenCleanupService` purges expired rows every 6 hours.
- Signing in mid-form remounts the calculator and loses typed figures. Accepted: the sign-in prompts sit before data entry in the natural flow. If it stings, state-stashing is its own issue.
- The login screen can no longer be mistaken for an access barrier. An operator who wants a truly private instance must put real authentication in front (it never actually had one — guest access was self-service).

## Alternatives considered

- **Lazy guest issuance** (mint only on the first API-needing action). Buys nothing: the landing page's state list is itself an authorized API call, so "first action" is page load.
- **Keep login-first with a bigger "Continue as Guest" button.** Still charges the majority audience a click and a decision for zero security.
- **Anonymous API access for calculations.** Would drop the session model entirely for guests; rejected because rate limiting, activity middleware, and the single bearer-token code path are worth keeping uniform.
