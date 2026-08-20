# ADR 0004 — External-OAuth-only public accounts

**Status:** Accepted — 2026-08-20

## Context

ADR 0002 left FairShare with no self-serve sign-up: accounts are admin-provisioned, so the gated features have no public conversion path. Opening sign-up on a site holding child-support financials makes FairShare a credential custodian — and local passwords on this data would demand reset email, 2FA, lockout policy, and breach posture, all operated by one person. The owner's stated bar: the user's sensitive information must be as private as possible, and that must be *visibly* true.

## Decision

Public accounts are **free and external-OAuth-only**; FairShare never holds a public user's password.

1. **Google is the only provider at launch** (working code ports from Portfolio). Microsoft can be added on real demand (free); Apple only if demand ever justifies the $99/yr developer program — iPhone users overwhelmingly have Google accounts anyway. Facebook: never, on privacy optics alone.
2. **We store only the Google subject ID and email.** Display name defaults to the email's local part and is user-editable. Nothing else Google offers (name, photo, locale) is kept.
3. **The local password form survives only for admin-provisioned accounts**, demoted behind an "Administrator / legacy sign-in" link; the Google button is the sign-in experience. The existing self-serve local registration (`/register`, `POST /auth/register`, gated behind `Auth:AllowSelfRegistration` — default off, and off in production) is **retired**: page and endpoint removed rather than left as a dormant password door.
4. **"Remember this device" is an explicit opt-in checkbox**, default unchecked: unchecked issues a session-only cookie, checked issues the 30-day rotating refresh cookie. Shared family computers — possibly shared with the other parent in the case — are the default assumption.
5. **Guest work carries over on the user's say-so.** In-progress work is stashed across the OAuth redirect and an explicit prompt offers to save it — on *every* sign-in with unsaved work, not just the first. The uniform rule, and the headline privacy claim: *nothing you type is stored unless you choose to save it; nothing is lost without asking*.
6. **Self-service hard delete** ("Delete my account and all my data") is a launch requirement for public sign-up, not a follow-up.
7. **TOTP 2FA is required for Admin-role local accounts**; family local accounts stay password-only (they guard only their own data), and public users inherit 2FA from Google.

The account's value proposition is continuity only — your saved profiles, back on any device — pitched in exactly those words, with no roadmap promises.

## Consequences

- No password-reset, credential-2FA, or lockout infrastructure to build, and no password hashes to breach. Google's account security (resets, suspicious-login detection) is inherited for free.
- A privacy-minded user unwilling to link Google has no way in. Accepted for launch; local passwords are reconsidered only on real user demand (Identity's schema keeps both doors open, so this is reversible).
- Public sign-in availability is coupled to Google; the admin's local escape hatch is the mitigation.
- Audit events (ADR 0003) record account lifecycle with the identity as written at the time and outlive deletion until their ~1-year retention expires — disclosed plainly on the privacy page. A hostile account cannot erase its own trail by self-deleting.
- No IP banning is built: per-IP rate limiting and the existing account-disable flag cover realistic abuse, and residential IP bans punish innocents. Revisit only on evidence.

## Alternatives considered

- **OAuth + optional email/password.** The password apparatus is the single largest chunk of work and risk in the whole accounts effort, built speculatively for users who may not exist. Deferred until asked for.
- **Migrating admin/family to Google and deleting local login.** Cleaner surface, but couples admin access to Google availability and a personal Google account. The escape hatch stays.
- **Silent auto-save of guest work at sign-in.** Less friction, but it would make the site's strongest privacy sentence false at the exact moment of conversion.
- **Unconditional 30-day persistence (status quo).** Quietly assumes every device is private — the one assumption this site must not make.
