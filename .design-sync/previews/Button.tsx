import { Button, WarmProvider } from "@fairshare/warm-counsel";
import type { ReactNode } from "react";

// Every cell composes through WarmProvider - the DS's own contract ("wrap the
// whole screen in exactly one WarmProvider"); bare components inherit the host
// page's fonts and background instead of the Warm Counsel scope.
const Frame = ({ theme, children }: { theme?: "light" | "dark"; children: ReactNode }) => (
  <WarmProvider theme={theme}>
    <div style={{ padding: 16, display: "flex", gap: 12, alignItems: "center" }}>{children}</div>
  </WarmProvider>
);

/** The one main action per view: filled terracotta. */
export const Primary = () => <Frame><Button>Calculate</Button></Frame>;

/** Secondary action (Export-style) and destructive action (Reset-style). */
export const OutlineVariants = () => (
  <Frame>
    <Button variant="outline-accent">Export to Excel</Button>
    <Button variant="outline-danger">Reset</Button>
  </Frame>
);

/** Disabled reads as inert - dimmed, not-allowed cursor. */
export const Disabled = () => (
  <Frame>
    <Button disabled>Calculate</Button>
    <Button variant="outline-accent" disabled>Export to Excel</Button>
  </Frame>
);

/** Every variant on the espresso theme. */
export const DarkTheme = () => (
  <Frame theme="dark">
    <Button>Calculate</Button>
    <Button variant="outline-accent">Export to Excel</Button>
    <Button variant="outline-danger">Reset</Button>
  </Frame>
);
