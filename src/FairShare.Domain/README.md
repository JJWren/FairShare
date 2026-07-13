# FairShare.Domain

The pure calculation engine. No ASP.NET, no EF, no Identity — just deterministic child-support math that both the API (and, in principle, any other host) can call. If you're changing how support is computed, this is the only project you should need to touch.

## Layout

- `Calculators/`
  - `BaseChildSupportCalculator` — shared validation and calculation plumbing.
  - `CS42Calculator` — Alabama CS-42 (standard custody) worksheet.
  - `CS42SCalculator` — Alabama CS-42-S (shared parenting) worksheet.
- `Seeds/BcsoLookup.cs` — Alabama's Basic Child Support Obligation schedule (the income × children lookup table the worksheets are built on).
- `Models/ParentData.cs` — plain input model (income, preexisting support/alimony, childcare and healthcare costs, custody flag).
- `Helpers/` — `CalculationResult` (success flag, payer, final amount), `CalcError` (code/message/field/severity), shared enums.
- `Interfaces/` + `Services/StateGuidelineCatalog.cs` — `IChildSupportCalculator` is the contract every worksheet implements; the catalog maps `(state, form)` → calculator and backs the API's `/states` endpoints.

## Adding a new state or form

1. Implement `IChildSupportCalculator` (extend `BaseChildSupportCalculator` where the plumbing fits).
2. Register it in DI (see the "Domain services" block in `FairShare.Api/Program.cs`) — `StateGuidelineCatalog` discovers calculators from DI, and the new form shows up in the catalog endpoints automatically.
3. Add unit tests in `FairShare.Tests/Domain/` — calculators are pure, so tests are plain input/output assertions with no test host.

## Rules

- Keep this project dependency-free (no web/persistence packages). Anything that needs HTTP or a database belongs in `FairShare.Api`.
- Calculators must be deterministic and side-effect-free; report input problems via `CalcError` entries on the result, don't throw.
