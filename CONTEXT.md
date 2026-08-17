# FairShare — Domain Glossary

The ubiquitous language of the FairShare domain: child-support worksheets as the courts define them. Code, tests, docs and issues use these terms with exactly these meanings. This file is a glossary only — no implementation detail lives here.

## Forms and their source

| Term | Meaning |
|---|---|
| **AOC** | Alabama Administrative Office of Courts — publishes the child-support forms (eforms.alacourt.gov) and the Excel workbooks that compute them. |
| **CS-42** | AOC Form CS-42 (Rev. 5/2022), the *standard-custody* worksheet: one parent has primary physical custody and the other pays. |
| **CS-42-S** | AOC Form CS-42-S (Eff. 6/2023), the *shared 50% physical-custody* worksheet: the children live equally with both parents; the higher net obligation pays. |
| **Official workbook** | The AOC's Excel version of a form. Its formulas are the reference implementation FairShare mirrors; when FairShare and the workbook disagree, the workbook is right. |
| **Template workbook** | A copy of an official workbook that FairShare fills in with a user's inputs (formulas untouched). |
| **Form key** | The short identifier for a form in routes and requests: `CS42`, `CS42S`. |
| **Display name** | The human-readable form name including its revision, e.g. "CS-42 (Rev. 5/2022)". |

## Worksheet vocabulary

| Term | Meaning |
|---|---|
| **Worksheet line** | One numbered row of a form ("1", "1a", "1b", "2" … "13"/"14"), with a Plaintiff, Defendant and/or Combined cell exactly as printed. FairShare returns every line so a user can compare it with the paper form. |
| **Plaintiff / Defendant** | The two parents as named on the court order; the form's two money columns. |
| **Gross income** | Line 1: monthly gross income before any deduction. |
| **Preexisting payments** | Lines 1a/1b: child support and periodic alimony already ordered in other cases, deducted from gross income. |
| **Adjusted gross income (AGI)** | Line 2: gross income minus preexisting payments, per parent. |
| **Combined adjusted gross income (CAGI)** | Line 2, Combined column: both parents' AGI added together; the number the schedule is looked up on. |
| **Percentage share of income** | Line 3: each parent's AGI as a fraction of CAGI, rounded to whole percent the way the workbook does (one parent rounded, the other is the remainder). |
| **Schedule** | The AOC *Schedule of Basic Child-Support Obligations* ("AL Realigned Sept 2021"): CAGI in $50 brackets from $250 to $30,000 × 1–6 children → a monthly amount. Above the top bracket the guidelines leave support to the court's discretion. |
| **Basic child-support obligation (BCSO)** | Line 4: the schedule amount for the CAGI bracket and number of children. |
| **Work-related child-care costs / Health-care-coverage costs** | The two cost lines each parent pays for the children, added to the obligation and credited back to whoever pays them. |
| **Self-support reserve (SSR)** | CS-42 line 11: $981 a month of AGI kept back for the payer's own support before support is measured. |
| **Minimum obligation** | CS-42 line 12: a parent with any gross income owes at least $50; a parent with no gross income owes $0. |
| **Shared 50% physical-custody obligation** | CS-42-S line 5: 150% of the BCSO, reflecting two households. |
| **Shared-custody credit** | CS-42-S line 12: half of the shared obligation credited to each parent for the time the children are with them. |
| **Recommended child-support order** | The last line of either form: the amount the paying parent owes per month. On CS-42 the order applies to the non-custodial parent; on CS-42-S it is the higher line-13 amount, placed in that parent's column. |
| **Payer** | The parent the recommended order applies to. "No net transfer" means neither parent owes the other. |
| **Custodial parent** | On CS-42, the parent with primary physical custody; the other parent is the payer. CS-42-S has no custodial parent. |

## Fidelity vocabulary

| Term | Meaning |
|---|---|
| **Excel rounding** | `ROUND` as Excel does it — halves away from zero — the rounding every worksheet line uses. |
| **Golden case** | A set of inputs whose expected values on every line were read back from the official workbook; the calculators are pinned to them. |
| **Oracle test** | A test that fills a template workbook with a case's inputs, recalculates it, and compares each cell with FairShare's line values. |
