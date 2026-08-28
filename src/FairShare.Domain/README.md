# FairShare.Domain

The pure calculation engine. No ASP.NET, no EF, no Identity — just deterministic child-support math that both the API (and, in principle, any other host) can call. If you're changing how support is computed, this is the only project you should need to touch.

## Layout

- `Calculators/`
  - `BaseChildSupportCalculator` — the template: validates the child count against the form's `Schedule`, runs the form's `BuildWorksheet`, maps `IncomeAboveScheduleException` / unexpected failures to `CalcError`s, and provides the Excel-faithful helpers (`ExcelRound`, `ExcelRound2`, `ShareOfIncome`, `AddIncomeLines` for lines 1–2). It is state-agnostic: schedule data, child-count bounds and above-ceiling behavior all come from the calculator's `IObligationSchedule` (ADR 0005).
  - `WorksheetBuilder` — collects `WorksheetLine`s in form order.
  - `CS42Calculator` — Alabama CS-42 (Rev. 5/2022, standard custody), lines 1–13.
  - `CS42SCalculator` — Alabama CS-42-S (Eff. 6/2023, shared 50/50 physical custody), lines 1–14.
  - `OregonWorksheetCalculator` — the Oregon Child Support Worksheet (CSF 02 0910, OAR 137-050), lines 1a–9g including Children Attending School, medical support and the parenting-time credit. Takes `OregonWorksheetInput` (richer than `ParentData`) and returns `OregonCalculationOutcome` (both parents can owe at once). It implements `IWorksheetForm` directly — not `IChildSupportCalculator`, whose two-parents-plus-child-count shape doesn't fit — and is in the catalog and live behind `/States/OR`. Golden cases come from the official DOJ workbook via `Generate-OregonGoldenCases.ps1`.
- `Seeds/` — per-state schedule data. `BcsoLookup.cs` is Alabama's Schedule of Basic Child-Support Obligations ("AL Realigned Sept 2021"): `Get(cagi, children)` reproduces the workbook's lookup — `MAX(250, ROUND(cagi/50)*50)`, halves away from zero; a bracket above $30,000 throws `IncomeAboveScheduleException` with Alabama's wording. `AlabamaObligationSchedule` exposes it through `IObligationSchedule` for the two Alabama forms. `OregonScaleLookup.cs` is Oregon's Obligation Scale (OAR 137-050-0725 appendix, read from the official DOJ workbook): lower-bracket selection, flat $50 row below $1,001, and a rebuttable cap at the $30,000 row instead of an error — exposed via `OregonObligationSchedule`. `OregonRuleParameters.cs` carries Oregon's dated rule values (self-support reserve — re-set every July 1 — minimum order, medical cost cap, child-care caps).
- `Models/ParentData.cs` — plain input model (income, preexisting support/alimony, childcare and healthcare costs, custody flag).
- `Helpers/` — `CalculationResult` (success flag, payer, final amount, `Lines`), `WorksheetLine` (number/label/plaintiff/defendant/combined/format), `CalcError` + `CalcErrorCodes`, `FormInfo` (catalog summary), shared enums.
- `Interfaces/` + `Services/StateGuidelineCatalog.cs` — `IWorksheetForm` is the registration contract every form implements (`State`, `Form`, `DisplayName`, `Description`, `IsSharedCustody`); `IChildSupportCalculator` extends it with `Calculate(ParentData, ParentData, int)` for the classic two-parent forms (both Alabama forms). The catalog discovers every `IWorksheetForm` from DI, maps `(state, form)` → form, and backs the API's `/states` endpoints.

## How a calculator is written

The official AOC Excel workbook for a form is the reference implementation (see `docs/adr/0001-mirror-official-worksheet-lines.md` and the glossary in `CONTEXT.md`). A calculator walks the form top to bottom, computing every numbered line with the workbook's own formula and Excel's rounding (`MidpointRounding.AwayFromZero`), and returns who pays what plus every line. Don't "simplify" the arithmetic — a reviewer should be able to put the workbook next to the code and check line by line.

## Adding a new state or form

1. For a new state, implement its `IObligationSchedule` in `Seeds/` with that state's table data, child-count range, bracket-selection semantics and above-ceiling behavior (error vs. cap — see ADR 0005; the two contradict between states, so never share them).
2. Pick the calculator shape the form actually has:
   - **Classic two-parents-plus-child-count form** (both Alabama forms): extend `BaseChildSupportCalculator` — set `State`/`Form`/`DisplayName`/`Description`, point `Schedule` at the state's schedule, and implement `BuildWorksheet` line by line against the form's official workbook.
   - **Anything richer** (Oregon's worksheet: per-parent overnights, spousal support both directions, Children Attending School): implement `IWorksheetForm` directly with the form's own input/outcome models, the way `OregonWorksheetCalculator` does. Fair warning: `CalculationRunner` in the API currently dispatches on concrete types for the non-classic shape, so a new differently-shaped form also touches that seam.
3. Register it in DI (see the "Domain services" block in `FairShare.Api/Program.cs`) — `StateGuidelineCatalog` discovers every `IWorksheetForm` from DI, and the new form shows up in the catalog endpoints automatically.
4. Add golden cases in `FairShare.Tests/Domain/Golden/` whose expected values are read back from that form's official workbook (see `Generate-GoldenCases.ps1` / `Generate-OregonGoldenCases.ps1`), plus targeted unit tests in `FairShare.Tests/Domain/`.

## Rules

- Keep this project dependency-free (no web/persistence packages). Anything that needs HTTP or a database belongs in `FairShare.Api`.
- Calculators must be deterministic and side-effect-free; `Calculate` never throws — input problems come back as `CalcError` entries (`CalcErrorCodes`) on the result.
