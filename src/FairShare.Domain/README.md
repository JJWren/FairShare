# FairShare.Domain

The pure calculation engine. No ASP.NET, no EF, no Identity — just deterministic child-support math that both the API (and, in principle, any other host) can call. If you're changing how support is computed, this is the only project you should need to touch.

## Layout

- `Calculators/`
  - `BaseChildSupportCalculator` — the template: validates the child count against the form's `Schedule`, runs the form's `BuildWorksheet`, maps `IncomeAboveScheduleException` / unexpected failures to `CalcError`s, and provides the Excel-faithful helpers (`ExcelRound`, `ExcelRound2`, `ShareOfIncome`, `AddIncomeLines` for lines 1–2). It is state-agnostic: schedule data, child-count bounds and above-ceiling behavior all come from the calculator's `IObligationSchedule` (ADR 0005).
  - `WorksheetBuilder` — collects `WorksheetLine`s in form order.
  - `CS42Calculator` — Alabama CS-42 (Rev. 5/2022, standard custody), lines 1–13.
  - `CS42SCalculator` — Alabama CS-42-S (Eff. 6/2023, shared 50/50 physical custody), lines 1–14.
- `Seeds/` — per-state schedule data. `BcsoLookup.cs` is Alabama's Schedule of Basic Child-Support Obligations ("AL Realigned Sept 2021"): `Get(cagi, children)` reproduces the workbook's lookup — `MAX(250, ROUND(cagi/50)*50)`, halves away from zero; a bracket above $30,000 throws `IncomeAboveScheduleException` with Alabama's wording. `AlabamaObligationSchedule` exposes it through `IObligationSchedule` for the two Alabama forms.
- `Models/ParentData.cs` — plain input model (income, preexisting support/alimony, childcare and healthcare costs, custody flag).
- `Helpers/` — `CalculationResult` (success flag, payer, final amount, `Lines`), `WorksheetLine` (number/label/plaintiff/defendant/combined/format), `CalcError` + `CalcErrorCodes`, `FormInfo` (catalog summary), shared enums.
- `Interfaces/` + `Services/StateGuidelineCatalog.cs` — `IChildSupportCalculator` is the contract every worksheet implements (`State`, `Form`, `DisplayName`, `Description`, `IsSharedCustody`, `Calculate`); the catalog maps `(state, form)` → calculator and backs the API's `/states` endpoints.

## How a calculator is written

The official AOC Excel workbook for a form is the reference implementation (see `docs/adr/0001-mirror-official-worksheet-lines.md` and the glossary in `CONTEXT.md`). A calculator walks the form top to bottom, computing every numbered line with the workbook's own formula and Excel's rounding (`MidpointRounding.AwayFromZero`), and returns who pays what plus every line. Don't "simplify" the arithmetic — a reviewer should be able to put the workbook next to the code and check line by line.

## Adding a new state or form

1. For a new state, implement its `IObligationSchedule` in `Seeds/` with that state's table data, child-count range, bracket-selection semantics and above-ceiling behavior (error vs. cap — see ADR 0005; the two contradict between states, so never share them).
2. Extend `BaseChildSupportCalculator`: set `State`/`Form`/`DisplayName`/`Description`, point `Schedule` at the state's schedule, and implement `BuildWorksheet` line by line against the form's official workbook.
3. Register it in DI (see the "Domain services" block in `FairShare.Api/Program.cs`) — `StateGuidelineCatalog` discovers calculators from DI, and the new form shows up in the catalog endpoints automatically.
4. Add golden cases in `FairShare.Tests/Domain/Golden/` whose expected values are read back from that form's official workbook (see `Generate-GoldenCases.ps1`), plus targeted unit tests in `FairShare.Tests/Domain/`.

## Rules

- Keep this project dependency-free (no web/persistence packages). Anything that needs HTTP or a database belongs in `FairShare.Api`.
- Calculators must be deterministic and side-effect-free; `Calculate` never throws — input problems come back as `CalcError` entries (`CalcErrorCodes`) on the result.
