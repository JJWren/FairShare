# Changelog

All notable changes to this project will be documented in this file.

> **Versioning re-baseline (2026-07-13):** releases were renumbered onto a clean semver line —
> the former 7.0.1 became **1.0.0**, 7.1.0 became **1.1.0**, and 8.0.0 became **2.0.0**.
> Everything older than 1.0.0 (the former 0.1.0–7.0.0 releases) is unsupported and was retired
> along with its tags and release pages.

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
