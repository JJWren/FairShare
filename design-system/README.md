# @fairshare/warm-counsel

FairShare's **Warm Counsel** design system (direction picked 2026-08-28, issue #183):
`src/styles/tokens.css` is the single source of truth for the visual identity — cream/espresso
neutrals, terracotta accent, Lora + Karla, and the app's hand-tuned plaintiff/defendant/gold
party palette, in light and dark.

Two consumers, one token file:

- **The Blazor app** (#184) imports `tokens.css` and styles Razor components from the same
  `var(--fs-*)` variables. This package's React components never ship in the app.
- **Claude Design** consumes the compiled React components (design-time twins), so design
  work there composes FairShare's real parts. Synced via `/design-sync`.

```bash
npm install
npm run build   # esbuild bundle + .d.ts
npm test        # WCAG AA assertion over every token pair, both themes
```

The contrast test is the same assertion set that gated the approved mocks — a token change
that breaks AA fails the build, in either theme.
