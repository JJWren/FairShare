import { MoneyInput, WarmProvider } from "@fairshare/warm-counsel";
import type { ReactNode } from "react";

// WarmProvider on every cell: the DS contract, and what gives the fields the
// Karla type scope and cream page background.
const Frame = ({ theme, children }: { theme?: "light" | "dark"; children: ReactNode }) => (
  <WarmProvider theme={theme}>
    <div style={{ padding: 16, display: "grid", gap: 16, maxWidth: 380 }}>{children}</div>
  </WarmProvider>
);

/** The canonical money field, as on the CS-42 card. */
export const Basic = () => (
  <Frame><MoneyInput label="Monthly gross income" placeholder="0" defaultValue="3000" /></Frame>
);

/** With the helper line screen readers get via aria-describedby. */
export const WithHint = () => (
  <Frame>
    <MoneyInput
      label="Work-related child-care costs"
      hint="Costs due to employment or job search, for the children of this case."
      placeholder="0"
    />
  </Frame>
);

/** Empty-by-default: untouched fields show the 0 placeholder, never a prefilled 0. */
export const Empty = () => (
  <Frame>
    <MoneyInput
      label="Preexisting child-support payments"
      hint="Amounts actually paid under earlier orders for other children."
      placeholder="0"
    />
  </Frame>
);

/** The field set on espresso. */
export const DarkTheme = () => (
  <Frame theme="dark">
    <MoneyInput label="Monthly gross income" placeholder="0" defaultValue="3000" />
    <MoneyInput
      label="Health-care coverage costs"
      hint="The children's share of the premium only."
      placeholder="0"
    />
  </Frame>
);
