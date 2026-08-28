# Warm Counsel — how to build with it

FairShare's design system: a court-adjacent, warm support-tool identity (cream page,
espresso dark theme, Lora display + Karla body, terracotta primary). These conventions
are load-bearing — every one of them is how the shipped Blazor app actually behaves.

- **Wrap every screen in exactly one `WarmProvider`** (`theme="light"` default,
  `"dark"` for espresso). It applies the page background, ink color, and the type
  scope; bare components inherit the host page's fonts and background instead.
- **Party colors are a fixed convention**: plaintiff is always teal, defendant always
  violet (`PartyBadge`, `PartyCard`), and color is never the only signal — the badge
  text carries the meaning. Both pairs are WCAG AA in both themes; don't restyle them.
- **One primary `Button` per view** (filled terracotta — the Calculate action);
  `outline-accent` for secondary actions like Export, `outline-danger` for
  destructive ones like Reset. 44px hit targets are built in.
- **Every dollar figure goes through `MoneyInput`**: $ prefix, decimal inputmode, and
  a `hint` that reaches screen readers via aria-describedby. Fields are
  empty-by-default with a `0` placeholder — never prefill a literal 0.
- **`WorksheetTable` is the centerpiece**: line numbers must match the official court
  form, empty cells render em dashes, and `highlight` marks exactly one row — the
  recommended-order line — with the gold treatment.
- **`Toggle` is for boolean case facts** ("Primary custody"). Omitting `onChange`
  renders it display-only (aria-disabled, not focusable) — use that for read-only
  summaries, never a fake-operable control.
- **Fonts**: Lora and Karla load through the stylesheet's Google Fonts import; tokens
  are `--fs-*` custom properties defined for both themes.
