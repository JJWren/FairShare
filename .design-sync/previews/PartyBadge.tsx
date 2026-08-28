import { PartyBadge, WarmProvider } from "@fairshare/warm-counsel";
import type { ReactNode } from "react";

const Frame = ({ theme, children }: { theme?: "light" | "dark"; children: ReactNode }) => (
  <WarmProvider theme={theme}>
    <div style={{ padding: 16, display: "flex", gap: 12, alignItems: "center" }}>{children}</div>
  </WarmProvider>
);

/** The core convention: plaintiff teal, defendant violet - text always carries the meaning. */
export const BothParties = () => (
  <Frame>
    <PartyBadge party="plaintiff" />
    <PartyBadge party="defendant" />
  </Frame>
);

/** Custom labels keep the party color; useful when the case names the parents. */
export const CustomLabels = () => (
  <Frame>
    <PartyBadge party="plaintiff">Plaintiff - Mother</PartyBadge>
    <PartyBadge party="defendant">Defendant - Father</PartyBadge>
  </Frame>
);

/** Both pairs stay WCAG AA on espresso. */
export const DarkTheme = () => (
  <Frame theme="dark">
    <PartyBadge party="plaintiff" />
    <PartyBadge party="defendant" />
  </Frame>
);
