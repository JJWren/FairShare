# Changelog

All notable changes to this project will be documented in this file.

## [5.0.0](https://github.com/JJWren/FairShare/compare/fairshare-v4.0.0...fairshare-v5.0.0) (2026-05-06)

### ⚠ BREAKING CHANGES

* **Architecture**: Complete migration from ASP.NET Core MVC to Blazor WebAssembly SPA.
* **Tech Stack**: Updated to .NET 10 (Preview).
* **Identity**: Moved from Razor Pages to Pure Blazor Identity components with REST API backend.
* **Namespace**: Renamed projects and namespaces to `AppBackend`, `AppFrontend`, and `AppShared` to resolve platform-specific build collisions.

### Features

* **Blazor WASM**: High-performance Single Page Application frontend.
* **Client-side Logic**: Calculator logic now runs locally in the browser via WebAssembly for instant feedback.
* **Modernized UI**: Refined Bootstrap 5 layout with dynamic theme switching and responsive components.
* **Global Standards**: Integrated AIDLC development lifecycle and engineering standards.
* **Enhanced DevOps**: Robust CI/CD pipeline and multi-project Docker builds.


## [4.0.0](https://github.com/JJWren/FairShare/compare/fairshare-v3.0.0...fairshare-v4.0.0) (2025-10-08)


### ⚠ BREAKING CHANGES

* Implement user management and authorization features ([#17](https://github.com/JJWren/FairShare/issues/17))

### Features

* Implement user management and authorization features ([#17](https://github.com/JJWren/FairShare/issues/17)) ([413fc4f](https://github.com/JJWren/FairShare/commit/413fc4feda1073dadedd55933a147c69b9f68b77))

## [3.0.0](https://github.com/JJWren/FairShare/compare/fairshare-v2.0.0...fairshare-v3.0.0) (2025-10-07)


### ⚠ BREAKING CHANGES

* Add parent profile management and database support ([#13](https://github.com/JJWren/FairShare/issues/13))

### Features

* Add parent profile management and database support ([#13](https://github.com/JJWren/FairShare/issues/13)) ([8bd586a](https://github.com/JJWren/FairShare/commit/8bd586a2f251e82ad64aec1637d5985d02efb589))

## [2.0.0](https://github.com/JJWren/FairShare/compare/fairshare-v1.0.0...fairshare-v2.0.0) (2025-10-06)


### ⚠ BREAKING CHANGES

* Overhauled the views again O_O ([#10](https://github.com/JJWren/FairShare/issues/10))

### Features

* Overhauled the views again O_O ([#10](https://github.com/JJWren/FairShare/issues/10)) ([f619222](https://github.com/JJWren/FairShare/commit/f619222eabad975695bccaf79ae7b4b7c0623104))

## [1.0.0](https://github.com/JJWren/FairShare/compare/fairshare-v0.1.1...fairshare-v1.0.0) (2025-10-05)


### ⚠ BREAKING CHANGES

* Revamped view and controller organization to prepare for additional states and forms.
* Work in progress.

### Features

* Revamped view and controller organization to prepare for additional states and forms. ([eac7111](https://github.com/JJWren/FairShare/commit/eac7111fc82742ac1ac7883cc8f17dd0e3863c15))
* Work in progress. ([800d24d](https://github.com/JJWren/FairShare/commit/800d24d48989c5e8cc724d2752ca85cf7e64754c))

## [0.1.1](https://github.com/JJWren/FairShare/compare/fairshare-v0.1.0...fairshare-v0.1.1) (2025-10-04)


### Bug Fixes

* **ci:** Create .release-please-manifest.json ([3dd6eea](https://github.com/JJWren/FairShare/commit/3dd6eea74701807641c5aadb5fdffd636ca8ff92))
* **ci:** Update release-please-config.json ([cda3766](https://github.com/JJWren/FairShare/commit/cda37665627238feaf6fcf40f8a4d9a547007a89))
* **ci:** Update release.yml ([a78b5d1](https://github.com/JJWren/FairShare/commit/a78b5d181272b3eca2536514a7099be93e4c4404))
* **ci:** Update release.yml ([5b87b47](https://github.com/JJWren/FairShare/commit/5b87b4785317407f48c95d2b0140513e29c44210))

## [Unreleased]

## [0.1.0] - 2025-10-03
### Added
- Initial release with CS-42-S (SPCA) calculator
- Dockerfile & docker-compose
- Health endpoint, error pages

[Unreleased]: https://github.com/JJWren/FairShare/compare/0.1.0...HEAD
