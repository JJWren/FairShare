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
| **Estimate** | What FairShare produces: a calculation of the recommended order under a state's published guidelines. Never a court order — only a court sets an actual support obligation. |
| **Payer** | The parent the recommended order applies to. "No net transfer" means neither parent owes the other. |
| **Custodial parent** | On CS-42, the parent with primary physical custody; the other parent is the payer. CS-42-S has no custodial parent. |

## Oregon vocabulary (OAR 137-050)

| Term | Meaning |
|---|---|
| **OAR 137-050** | Oregon's child-support guideline rules (division 50, 137-050-0700 through -0765), published by the Oregon DOJ; the source Oregon estimates mirror. |
| **Oregon workbook** | The Oregon DOJ's Guidelines Calculator Excel workbook — Oregon's official workbook: the reference implementation Oregon estimates are pinned to. |
| **Adjusted income** | A parent's income minus union dues, the parent's own health-insurance cost, and spousal support owed, plus spousal support received, minus the non-joint-child deduction. Income shares are computed on it. |
| **Joint child / Non-joint child** | A joint child is a child of both parents in the calculation. A non-joint child is a parent's other legal child, earning that parent a deduction computed from the scale itself. |
| **Self-support reserve (Oregon)** | The monthly amount of adjusted income kept back for a parent's own support before any obligation; re-set every July 1 ($1,729 effective 2026-07-01). |
| **Available income** | Adjusted income minus the self-support reserve; the ceiling a parent's total obligation may not exceed. |
| **Scale** | Oregon's Obligation Scale: combined adjusted income in $50 brackets to $30,000 × 1–10 children. Between brackets the *lower* row applies — no interpolation; above $30,000 the top row applies, rebuttably. |
| **Basic support obligation (Oregon)** | The scale amount for the combined adjusted income and number of joint children, divided between the parents by income share. |
| **Overnight** | The unit of parenting time: an average annual count per parent over two years, with half-day equivalents for schedules without overnights. |
| **Parenting time credit** | The credit against a parent's obligation that grows continuously with their overnights (a logistic curve — no cliff thresholds), applied only to the minor children's portion. |
| **Child Attending School (CAS)** | An unmarried joint child aged 18–20 in school at least half time. Counted in the obligation, excluded from the parenting-time-credit base, and paid their share of support directly. |
| **Medical support** | The health-coverage part of an order: who provides the children's coverage, each parent's share of its cost, or **cash medical support** when neither parent has coverage available. |
| **Reasonable in cost** | The cap on a parent's medical-support contribution: 4% of that parent's adjusted income, and $0 for a parent earning at or below full-time highest Oregon minimum wage. |
| **Minimum order (Oregon)** | The rebuttable presumption that an obligor can pay at least $100 a month in total support, with defined $0 exceptions (e.g. incarceration, disability benefits as sole income). |
| **Rebuttal factor** | One of the OAR 137-050-0760 grounds a court may use to move an order away from the guideline amount. |
| **Agreed support amount** | A stipulated order within ±15% of the guideline amount, presumed just and appropriate. |
| **Rule version** | The effective date of the guideline rules an estimate implements (e.g. "OAR 137-050 effective 2026-07-01"). Every Oregon estimate and every Scenario names its rule version. |

## Access vocabulary

| Term | Meaning |
|---|---|
| **Guest** | The default identity — anyone who has not signed in. A guest can calculate and export, never persist. |
| **User / Admin** | Signed-in accounts — a user signs in with an outside identity provider (or is admin-provisioned); an account is free. A user additionally saves parent profiles; an admin additionally manages accounts. |
| **Guest work** | A guest's in-progress calculation. It lives only in their browser and is never stored unless they sign in and choose to save it. |
| **Gated feature** | A capability reserved for users (e.g. saving a profile). The moment a guest reaches one is the invitation to sign in. |
| **Scenario** | A named, saved snapshot of one worksheet's inputs for a state and form, stamped with the rule version and the result they produced. Never call this a "case" — only a court has cases. |

## Observability vocabulary

| Term | Meaning |
|---|---|
| **Daily visitor** | A person counted at most once per UTC day by an anonymous key that cannot connect them across days. Never call this a "unique visitor" or "returning visitor" — FairShare cannot know either. |
| **Audit event** | A record that an account did something accountability cares about (signed in, was created, changed, or deleted). Kept longer than diagnostic logs; outlives the account it names until it expires. |
| **Diagnostic log** | A record of what the system did, kept briefly for troubleshooting. Never contains case content, names, or money amounts. |
| **Verbose mode** | A temporary admin-switched state in which diagnostic logs capture extra detail. It always turns itself back off. |

## Fidelity vocabulary

| Term | Meaning |
|---|---|
| **Excel rounding** | `ROUND` as Excel does it — halves away from zero — the rounding every worksheet line uses. |
| **Golden case** | A set of inputs whose expected values on every line were read back from the official workbook; the calculators are pinned to them. |
| **Oracle test** | A test that fills a template workbook with a case's inputs, recalculates it, and compares each cell with FairShare's line values. |
