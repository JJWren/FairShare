# ADR 0003 — First-party cookieless analytics on a WASM SPA

**Status:** Accepted — 2026-08-20

## Context

ADR 0002 made a bet — guest-first landing with gated persistence — and nothing measures whether it is paying off. The owner wants engagement and conversion-intent insight (are visitors calculating, finishing, hitting the gates?) plus basic reach. FairShare handles child-support financials, so the analytics must be beyond reproach: the site's core promise is privacy, and the measurement must not undercut it. Portfolio's first-party cookieless analytics (its ADR 0001) is the proven in-house precedent, but Portfolio is Blazor Server — its capture middleware sees every page request. FairShare is a standalone WASM SPA served by nginx: after first load, route changes never touch the API.

## Decision

Analytics are **first-party, cookieless, and content-free**, stored in the app's own SQLite database and visible only at `/admin/stats`.

1. **Capture**: API-backed actions record events server-side. Page views come from a tiny first-party beacon — the SPA router fires `POST /api/v1/analytics/page-views` on navigation. `DNT: 1` and `Sec-GPC: 1` suppress recording, checked on both client and server. No cookies, no pixels, no third-party script.
2. **Visitors**: counted as **Daily visitors** — `HMAC(per-install secret, UTC date + IP + UA)`. The date inside the hash makes cross-day linkage deliberately impossible; IP and user-agent are hash inputs only, never stored. Bot UAs (and empty UAs) are excluded; admin browsing is excluded.
3. **Events carry a name and a coarse target, never content.** v1 taxonomy: `calculation-started` / `calculation-completed` (target: form key), `gated-hit` (target: which gate), `donate-click` (via a first-party redirect); the accounts effort adds `sign-in`, `account-created`, `account-deleted`, `guest-work-imported`. No money amounts, no child counts, no case data — ever, including in targets.
4. **Retention**: a nightly rollup service aggregates raw rows into daily stat tables and deletes raw rows after 90 days; aggregates are kept; "today" is computed live from raw rows.
5. **Related decision — logs share the philosophy**: diagnostic logs and audit events are captured by a small hand-rolled `ILoggerProvider` batching into SQLite (no Serilog), so the structural no-sensitive-data wall lives in code we own. Diagnostic logs keep 30 days; audit events keep ~1 year and outlive the accounts they name. Verbose mode is a runtime switch that auto-reverts (~4 h) and is itself an audit event.

## Consequences

- FairShare cannot borrow Portfolio's "nothing runs in your browser" claim — the beacon is client code. The truthful claim, stated on the privacy page: *our own code only, no third parties, no cookies, nothing identifying stored*.
- Cross-day uniques, retention curves, and "returning visitors" are impossible **by design**; the glossary bans those terms.
- Ad-blockers may eat the beacon; undercounting page views is accepted. API-backed events are unaffected.
- Guest refresh-token rows (ADR 0002's accidental, bot-polluted visit proxy) stop being anyone's analytics.
- Account deletion (ADR 0004) does not touch analytics — there is nothing linkable to delete — but audit events survive until their own retention expires, disclosed on the privacy page.

## Alternatives considered

- **Hosted analytics (Plausible/Umami container + JS snippet).** Same rejection as Portfolio's: an extra service to run, a script for ad-blockers to eat, and the privacy guarantee living in a vendor's defaults instead of our own code.
- **Anonymous cookie ID.** Accurate cross-day uniques, but a client-side identifier drags a child-support site into consent-banner territory. Rejected outright.
- **Scraping nginx access logs.** The static server never sees SPA route changes, and it would mean retaining raw IPs — the opposite of the point.
- **Serilog + SQLite sink for the log pipeline.** Industry-standard, but two new dependencies (one weakly maintained) for 17 call sites, and redaction guarantees are easiest to prove in a sink we wrote.
