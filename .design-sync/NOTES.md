# design-sync notes — FairShare Warm Counsel

- The DS package is `design-system/` (`@fairshare/warm-counsel`), private, esbuild +
  tsc build (`npm run build --prefix design-system`). The build also flattens
  `src/styles/index.css` → `dist/warm-counsel.css` (local @imports inlined, the
  Google-Fonts remote @import preserved) — that flattened file is `cfg.cssEntry`.
  Without it the converter finds no CSS ([CSS_RUNTIME]): the package ships styles
  as `src/styles/*.css`, never importable from the JS entry.
- **Every preview cell must wrap in `WarmProvider`** — the DS applies fonts and page
  background on `.fs-root` only (tokens are also on `:root`, so colors work bare but
  type falls back to browser serif). This bit the first solo pass: hints and table
  labels rendered in default serif until wrapped.
- Fonts are Lora + Karla via a Google-Fonts remote @import ([FONT_REMOTE],
  informational) — nothing to ship; the Blazor app self-hosts its own woff2 copies.
- `cardMode: "column"` is set for NavBar, Button, and WorksheetTable (full-width
  cells; Button's dark strip and the tables overflowed grid cells otherwise).
- Known render warns: none recorded (validate is clean).
- Toggle's `DisplayOnly` state is semantically disabled (aria-disabled, not
  focusable) but visually identical to the interactive state — the DS's current
  design, noted in its grade.

## Re-sync risks

- `dist/warm-counsel.css` must be rebuilt whenever `src/styles/*.css` changes —
  `cfg.buildCmd` does it; a stale dist ships stale tokens.
- Preview content embeds engine-true CS-42 numbers ($3,000/$3,000 → Defendant owes
  $425 under "AL Realigned Sept 2021"). If the Alabama schedule is ever revised,
  refresh `WorksheetTable.tsx`/`.review` content to a current-vintage case.
- The token file `design-system/src/styles/tokens.css` is test-enforced byte-identical
  against the app's `wwwroot/css/warm-counsel-tokens.css` (WarmCounselTokenSyncTests);
  change tokens only through that pairing.
- Build assumed node 22 + the repo-root playwright@1.55.0 install (chromium cached)
  for the render check.
