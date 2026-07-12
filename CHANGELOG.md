# Changelog

All notable changes to this project will be documented in this file.

## [8.0.0](https://github.com/JJWren/FairShare/compare/fairshare-v7.1.0...fairshare-v8.0.0) (2026-07-12)


### ⚠ BREAKING CHANGES

* self-registration is now disabled by default; set Auth__AllowSelfRegistration=true (ALLOW_SELF_REGISTRATION in .env) to restore the previous behavior.

### Features

* public hardening - rate limiting, registration gate, password management, CSP ([#63](https://github.com/JJWren/FairShare/issues/63)) ([12be688](https://github.com/JJWren/FairShare/commit/12be688796e5fd19ca75f4c3294ee7e2025f3885))

## [7.1.0](https://github.com/JJWren/FairShare/compare/fairshare-v7.0.1...fairshare-v7.1.0) (2026-07-10)


### Features

* update saved parents in place when re-saved under an existing name ([#61](https://github.com/JJWren/FairShare/issues/61)) ([de98c1d](https://github.com/JJWren/FairShare/commit/de98c1d4b2f5e81d57e7d192750cf56d57f22e9c))

## [7.0.1](https://github.com/JJWren/FairShare/compare/fairshare-v7.0.0...fairshare-v7.0.1) (2026-07-10)


### Bug Fixes

* theme toggle never worked - vendored Bootstrap was 5.1 ([#55](https://github.com/JJWren/FairShare/issues/55)) ([7de7819](https://github.com/JJWren/FairShare/commit/7de78191927001346fa7142ef0845f106ce8acd0))

## [7.0.0](https://github.com/JJWren/FairShare/compare/fairshare-v6.0.0...fairshare-v7.0.0) (2026-05-07)


### ⚠ BREAKING CHANGES

* Implement user management and authorization features ([#17](https://github.com/JJWren/FairShare/issues/17))
* Add parent profile management and database support ([#13](https://github.com/JJWren/FairShare/issues/13))
* Overhauled the views again O_O ([#10](https://github.com/JJWren/FairShare/issues/10))
* Revamped view and controller organization to prepare for additional states and forms.
* Work in progress.

### Features

* Add parent profile management and database support ([#13](https://github.com/JJWren/FairShare/issues/13)) ([8bd586a](https://github.com/JJWren/FairShare/commit/8bd586a2f251e82ad64aec1637d5985d02efb589))
* Implement user management and authorization features ([#17](https://github.com/JJWren/FairShare/issues/17)) ([413fc4f](https://github.com/JJWren/FairShare/commit/413fc4feda1073dadedd55933a147c69b9f68b77))
* Migrate architecture to Blazor WebAssembly v5.0.0 ([137a4a5](https://github.com/JJWren/FairShare/commit/137a4a5b22743f8973d9ec784cbf41bb28b7f288))
* Overhauled the views again O_O ([#10](https://github.com/JJWren/FairShare/issues/10)) ([f619222](https://github.com/JJWren/FairShare/commit/f619222eabad975695bccaf79ae7b4b7c0623104))
* Revamped view and controller organization to prepare for additional states and forms. ([eac7111](https://github.com/JJWren/FairShare/commit/eac7111fc82742ac1ac7883cc8f17dd0e3863c15))
* Work in progress. ([800d24d](https://github.com/JJWren/FairShare/commit/800d24d48989c5e8cc724d2752ca85cf7e64754c))


### Bug Fixes

* Add missing Authentication/Authorization middleware to pipeline ([5712bf6](https://github.com/JJWren/FairShare/commit/5712bf64c4bbd66d345b6da78cc3fd04b0b7bc8d))
* Align default database and backup paths with Docker volume structure ([92ea54f](https://github.com/JJWren/FairShare/commit/92ea54fb45d535171ddd2893f90ca3f5e57a6185))
* **ci:** Create .release-please-manifest.json ([3dd6eea](https://github.com/JJWren/FairShare/commit/3dd6eea74701807641c5aadb5fdffd636ca8ff92))
* **ci:** Update release-please-config.json ([cda3766](https://github.com/JJWren/FairShare/commit/cda37665627238feaf6fcf40f8a4d9a547007a89))
* **ci:** Update release.yml ([a78b5d1](https://github.com/JJWren/FairShare/commit/a78b5d181272b3eca2536514a7099be93e4c4404))
* **ci:** Update release.yml ([5b87b47](https://github.com/JJWren/FairShare/commit/5b87b4785317407f48c95d2b0140513e29c44210))
* Definitive architectural alignment with unique namespaces and matching folders ([fe3e0e9](https://github.com/JJWren/FairShare/commit/fe3e0e9dd37f4564161251b580c1adfc00a61f82))
* Disable ImplicitUsings and use explicit GlobalUsings.cs to resolve CI naming collisions ([30b07a9](https://github.com/JJWren/FairShare/commit/30b07a9f2a8c686e72b6bc33735f977a88cc4a84))
* Final definitive structure with AppBackend/Frontend/Shared namespaces and isolated folders ([261d32a](https://github.com/JJWren/FairShare/commit/261d32a53500fa1105c1c57d3b671269c2d6e9aa))
* Isolate source in src/ and clean up solution duplicates to resolve CI failures ([b8a6369](https://github.com/JJWren/FairShare/commit/b8a63697b44ef56afe0e0a21d77cbc5aec4b8343))
* Rename Data namespace to Persistence to definitively resolve CI naming conflicts ([1efafe5](https://github.com/JJWren/FairShare/commit/1efafe536b1d31913b46483ddb1474b7f5084119))
* Resolve definitive namespace ambiguity by separating AssemblyName from RootNamespace ([295b86d](https://github.com/JJWren/FairShare/commit/295b86d5665f66415a3483ff0fe217185a5ea633))
* Resolve namespace ambiguity on Linux runners using global:: prefix ([439403e](https://github.com/JJWren/FairShare/commit/439403e39502a3320b05ffb762953f9be566069d))
* Restore project references and finalize 3-tier architecture with explicit usings ([119965d](https://github.com/JJWren/FairShare/commit/119965dff347ae53c1938b28e310c6da2c1c03f8))
* Robust authentication routing, simplified Identity UI, and fixed logout crash ([d6ea793](https://github.com/JJWren/FairShare/commit/d6ea7936c824fa6f1fec02f2fa445617578d0966))
* Thoroughly resolve namespace vs assembly name ambiguity using global:: ([69e776e](https://github.com/JJWren/FairShare/commit/69e776e7cfadf57efb0326a3ea4399cc23937c12))
* Update CI workflow for .NET 10 and specify solution file ([12245d1](https://github.com/JJWren/FairShare/commit/12245d15664b68a856de0665a97ea595be37f1f4))
* Update Dockerfile to build solution root and handle multi-project dependencies ([5eae2e0](https://github.com/JJWren/FairShare/commit/5eae2e03961c7ce37135135a6f61159e77af2d01))
* updated packages ([bb1c1a7](https://github.com/JJWren/FairShare/commit/bb1c1a761aef74125773d1bf098ed38dc84bab31))

## [6.0.0](https://github.com/JJWren/FairShare/compare/fairshare-v5.0.0...fairshare-v6.0.0) (2026-05-07)


### ⚠ BREAKING CHANGES

* Implement user management and authorization features ([#17](https://github.com/JJWren/FairShare/issues/17))
* Add parent profile management and database support ([#13](https://github.com/JJWren/FairShare/issues/13))
* Overhauled the views again O_O ([#10](https://github.com/JJWren/FairShare/issues/10))
* Revamped view and controller organization to prepare for additional states and forms.
* Work in progress.

### Features

* Add parent profile management and database support ([#13](https://github.com/JJWren/FairShare/issues/13)) ([8bd586a](https://github.com/JJWren/FairShare/commit/8bd586a2f251e82ad64aec1637d5985d02efb589))
* Implement user management and authorization features ([#17](https://github.com/JJWren/FairShare/issues/17)) ([413fc4f](https://github.com/JJWren/FairShare/commit/413fc4feda1073dadedd55933a147c69b9f68b77))
* Migrate architecture to Blazor WebAssembly v5.0.0 ([137a4a5](https://github.com/JJWren/FairShare/commit/137a4a5b22743f8973d9ec784cbf41bb28b7f288))
* Overhauled the views again O_O ([#10](https://github.com/JJWren/FairShare/issues/10)) ([f619222](https://github.com/JJWren/FairShare/commit/f619222eabad975695bccaf79ae7b4b7c0623104))
* Revamped view and controller organization to prepare for additional states and forms. ([eac7111](https://github.com/JJWren/FairShare/commit/eac7111fc82742ac1ac7883cc8f17dd0e3863c15))
* Work in progress. ([800d24d](https://github.com/JJWren/FairShare/commit/800d24d48989c5e8cc724d2752ca85cf7e64754c))


### Bug Fixes

* Add missing Authentication/Authorization middleware to pipeline ([5712bf6](https://github.com/JJWren/FairShare/commit/5712bf64c4bbd66d345b6da78cc3fd04b0b7bc8d))
* Align default database and backup paths with Docker volume structure ([92ea54f](https://github.com/JJWren/FairShare/commit/92ea54fb45d535171ddd2893f90ca3f5e57a6185))
* **ci:** Create .release-please-manifest.json ([3dd6eea](https://github.com/JJWren/FairShare/commit/3dd6eea74701807641c5aadb5fdffd636ca8ff92))
* **ci:** Update release-please-config.json ([cda3766](https://github.com/JJWren/FairShare/commit/cda37665627238feaf6fcf40f8a4d9a547007a89))
* **ci:** Update release.yml ([a78b5d1](https://github.com/JJWren/FairShare/commit/a78b5d181272b3eca2536514a7099be93e4c4404))
* **ci:** Update release.yml ([5b87b47](https://github.com/JJWren/FairShare/commit/5b87b4785317407f48c95d2b0140513e29c44210))
* Definitive architectural alignment with unique namespaces and matching folders ([fe3e0e9](https://github.com/JJWren/FairShare/commit/fe3e0e9dd37f4564161251b580c1adfc00a61f82))
* Disable ImplicitUsings and use explicit GlobalUsings.cs to resolve CI naming collisions ([30b07a9](https://github.com/JJWren/FairShare/commit/30b07a9f2a8c686e72b6bc33735f977a88cc4a84))
* Final definitive structure with AppBackend/Frontend/Shared namespaces and isolated folders ([261d32a](https://github.com/JJWren/FairShare/commit/261d32a53500fa1105c1c57d3b671269c2d6e9aa))
* Isolate source in src/ and clean up solution duplicates to resolve CI failures ([b8a6369](https://github.com/JJWren/FairShare/commit/b8a63697b44ef56afe0e0a21d77cbc5aec4b8343))
* Rename Data namespace to Persistence to definitively resolve CI naming conflicts ([1efafe5](https://github.com/JJWren/FairShare/commit/1efafe536b1d31913b46483ddb1474b7f5084119))
* Resolve definitive namespace ambiguity by separating AssemblyName from RootNamespace ([295b86d](https://github.com/JJWren/FairShare/commit/295b86d5665f66415a3483ff0fe217185a5ea633))
* Resolve namespace ambiguity on Linux runners using global:: prefix ([439403e](https://github.com/JJWren/FairShare/commit/439403e39502a3320b05ffb762953f9be566069d))
* Restore project references and finalize 3-tier architecture with explicit usings ([119965d](https://github.com/JJWren/FairShare/commit/119965dff347ae53c1938b28e310c6da2c1c03f8))
* Thoroughly resolve namespace vs assembly name ambiguity using global:: ([69e776e](https://github.com/JJWren/FairShare/commit/69e776e7cfadf57efb0326a3ea4399cc23937c12))
* Update CI workflow for .NET 10 and specify solution file ([12245d1](https://github.com/JJWren/FairShare/commit/12245d15664b68a856de0665a97ea595be37f1f4))
* Update Dockerfile to build solution root and handle multi-project dependencies ([5eae2e0](https://github.com/JJWren/FairShare/commit/5eae2e03961c7ce37135135a6f61159e77af2d01))
* updated packages ([bb1c1a7](https://github.com/JJWren/FairShare/commit/bb1c1a761aef74125773d1bf098ed38dc84bab31))

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
