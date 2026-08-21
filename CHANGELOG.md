# Changelog

All notable changes to this project will be documented in this file.

> **Versioning re-baseline (2026-07-13):** releases were renumbered onto a clean semver line —
> the former 7.0.1 became **1.0.0**, 7.1.0 became **1.1.0**, and 8.0.0 became **2.0.0**.
> Everything older than 1.0.0 (the former 0.1.0–7.0.0 releases) is unsupported and was retired
> along with its tags and release pages.

## [4.0.1](https://github.com/JJWren/FairShare/compare/fairshare-v4.0.0...fairshare-v4.0.1) (2026-08-21)


### Bug Fixes

* rename admin stats events endpoint to dodge ad-blocker filter lists ([#108](https://github.com/JJWren/FairShare/issues/108)) ([cb4bb29](https://github.com/JJWren/FairShare/commit/cb4bb2915f9dda72605864e1bbeb535ee206b2f3))

## [4.0.0](https://github.com/JJWren/FairShare/compare/fairshare-v3.1.0...fairshare-v4.0.0) (2026-08-20)


### ⚠ BREAKING CHANGES

* POST /auth/register removed; GET /auth/config now returns { googleEnabled } instead of { allowSelfRegistration }.

### Features

* donations - Support page, footer link, first-party donate redirect ([#107](https://github.com/JJWren/FairShare/issues/107)) ([9b354de](https://github.com/JJWren/FairShare/commit/9b354debba26d6ef072f5b636d46301dad84d99a))
* public sign-in — Google OAuth, remember-device, carry-over, hard delete, admin TOTP ([#104](https://github.com/JJWren/FairShare/issues/104)) ([9566549](https://github.com/JJWren/FairShare/commit/9566549eb3b449128cfd976baac584294c21ed21))

## [3.1.0](https://github.com/JJWren/FairShare/compare/fairshare-v3.0.0...fairshare-v3.1.0) (2026-08-20)


### Features

* admin observability — persistent logs, audit trail, first-party analytics ([#101](https://github.com/JJWren/FairShare/issues/101)) ([85b4228](https://github.com/JJWren/FairShare/commit/85b42285066ac0436eac9b5518d16eec6bff0ebd))

## [3.0.0](https://github.com/JJWren/FairShare/compare/fairshare-v2.3.3...fairshare-v3.0.0) (2026-08-19)


### ⚠ BREAKING CHANGES

* default deployment posture changes from login-first to public calculator; the login page's Continue as Guest affordance is removed.

### Features

* distinct party, results, and action styling with form a11y ([#99](https://github.com/JJWren/FairShare/issues/99)) ([6600b58](https://github.com/JJWren/FairShare/commit/6600b5823d96875b811da38137b2f5879cde9e03)), closes [#95](https://github.com/JJWren/FairShare/issues/95)
* gate saved-parent features honestly for guests ([#98](https://github.com/JJWren/FairShare/issues/98)) ([3cec9d6](https://github.com/JJWren/FairShare/commit/3cec9d648707bad214964b4ec18b7952135d5f2c)), closes [#94](https://github.com/JJWren/FairShare/issues/94)
* land visitors as guests with sign-in as an account upgrade ([#96](https://github.com/JJWren/FairShare/issues/96)) ([1cda739](https://github.com/JJWren/FairShare/commit/1cda739bb815254ea00298fd78f282880a0e709e)), closes [#93](https://github.com/JJWren/FairShare/issues/93)

## [2.3.3](https://github.com/JJWren/FairShare/compare/fairshare-v2.3.2...fairshare-v2.3.3) (2026-08-19)


### Bug Fixes

* **deps:** bump Microsoft packages to 10.0.11 and adopt KnownIPNetworks ([#91](https://github.com/JJWren/FairShare/issues/91)) ([16ba9ca](https://github.com/JJWren/FairShare/commit/16ba9ca5584323f340862d91f489533a54dc8baf))

## [2.3.2](https://github.com/JJWren/FairShare/compare/fairshare-v2.3.1...fairshare-v2.3.2) (2026-08-19)


### Bug Fixes

* **web:** replace corrupted favicon with valid brand icon assets ([#89](https://github.com/JJWren/FairShare/issues/89)) ([dbf287d](https://github.com/JJWren/FairShare/commit/dbf287da5db2bef7beb02917b03aa891b3cc85ab))

## [2.3.1](https://github.com/JJWren/FairShare/compare/fairshare-v2.3.0...fairshare-v2.3.1) (2026-08-18)


### Bug Fixes

* **api:** skip HTTPS redirection behind the proxy; persist DataProtection keys ([#87](https://github.com/JJWren/FairShare/issues/87)) ([dcde9bc](https://github.com/JJWren/FairShare/commit/dcde9bcf1293093213ced04cd5f782ffec0eb89d)), closes [#85](https://github.com/JJWren/FairShare/issues/85)

## [2.3.0](https://github.com/JJWren/FairShare/compare/fairshare-v2.2.0...fairshare-v2.3.0) (2026-08-17)


### Features

* **web:** show the paying parent's name in the result sentence ([#82](https://github.com/JJWren/FairShare/issues/82)) ([07638c0](https://github.com/JJWren/FairShare/commit/07638c0e82f4eec2d1116224b6d15f726bba9ac9)), closes [#81](https://github.com/JJWren/FairShare/issues/81)


### Bug Fixes

* **web:** revalidate app assets, legible error banner, mirrored Primary Custody ([#79](https://github.com/JJWren/FairShare/issues/79)) ([612a368](https://github.com/JJWren/FairShare/commit/612a368e716338ae97de13b761cf8d3dfa223bf1)), closes [#78](https://github.com/JJWren/FairShare/issues/78)

## [2.2.0](https://github.com/JJWren/FairShare/compare/fairshare-v2.1.1...fairshare-v2.2.0) (2026-08-17)


### Features

* export the completed official Excel worksheet with FairShare's inputs ([#77](https://github.com/JJWren/FairShare/issues/77)) ([3f79585](https://github.com/JJWren/FairShare/commit/3f7958546ec967d616201a8c236ddce53c51ad3d)), closes [#73](https://github.com/JJWren/FairShare/issues/73)
* **web:** switch forms in place with preserved inputs and a worksheet table ([#76](https://github.com/JJWren/FairShare/issues/76)) ([4d04dfc](https://github.com/JJWren/FairShare/commit/4d04dfc39d0900584908ff7c93d1fa8e565ed53c)), closes [#72](https://github.com/JJWren/FairShare/issues/72)


### Bug Fixes

* **domain:** mirror the official CS-42 / CS-42-S worksheets line by line ([#74](https://github.com/JJWren/FairShare/issues/74)) ([08a127b](https://github.com/JJWren/FairShare/commit/08a127beed2ac3d3fd3a24fbd099b5cdeef3cdad)), closes [#71](https://github.com/JJWren/FairShare/issues/71)

## [2.1.1](https://github.com/JJWren/FairShare/compare/fairshare-v2.1.0...fairshare-v2.1.1) (2026-07-13)


### Bug Fixes

* **web:** stop immutable-caching the unfingerprinted Blazor entry scripts ([#69](https://github.com/JJWren/FairShare/issues/69)) ([caa2e3d](https://github.com/JJWren/FairShare/commit/caa2e3dbe93bf324cb1c051fc14a7eb9c1a91d30))

## [2.1.0](https://github.com/JJWren/FairShare/compare/fairshare-v2.0.0...fairshare-v2.1.0) (2026-07-13)


### Features

* **web:** show app version next to the footer copyright ([#67](https://github.com/JJWren/FairShare/issues/67)) ([c668af4](https://github.com/JJWren/FairShare/commit/c668af46930cf4d93dd227f23456ea1ad9eddbd2))

## [2.0.0](https://github.com/JJWren/FairShare/compare/fairshare-v1.1.0...fairshare-v2.0.0) (2026-07-12)


### ⚠ BREAKING CHANGES

* self-registration is now disabled by default; set Auth__AllowSelfRegistration=true (ALLOW_SELF_REGISTRATION in .env) to restore the previous behavior.

### Features

* public hardening - rate limiting, registration gate, password management, CSP ([#63](https://github.com/JJWren/FairShare/issues/63)) ([12be688](https://github.com/JJWren/FairShare/commit/12be688796e5fd19ca75f4c3294ee7e2025f3885))

## [1.1.0](https://github.com/JJWren/FairShare/compare/fairshare-v1.0.0...fairshare-v1.1.0) (2026-07-10)


### Features

* update saved parents in place when re-saved under an existing name ([#61](https://github.com/JJWren/FairShare/issues/61)) ([de98c1d](https://github.com/JJWren/FairShare/commit/de98c1d4b2f5e81d57e7d192750cf56d57f22e9c))

## [1.0.0](https://github.com/JJWren/FairShare/releases/tag/fairshare-v1.0.0) (2026-07-10)

First supported release: standalone Blazor WebAssembly SPA + decoupled REST API (JWT auth, ASP.NET Core, SQLite).


### Bug Fixes

* theme toggle never worked - vendored Bootstrap was 5.1 ([#55](https://github.com/JJWren/FairShare/issues/55)) ([7de7819](https://github.com/JJWren/FairShare/commit/7de78191927001346fa7142ef0845f106ce8acd0))
