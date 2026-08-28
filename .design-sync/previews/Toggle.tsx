import { Toggle, WarmProvider } from "@fairshare/warm-counsel";
import type { ReactNode } from "react";

const Frame = ({ theme, children }: { theme?: "light" | "dark"; children: ReactNode }) => (
  <WarmProvider theme={theme}>
    <div style={{ padding: 16, display: "grid", gap: 14, justifyItems: "start" }}>{children}</div>
  </WarmProvider>
);

/** On: the teal track marks the active state. */
export const On = () => <Frame><Toggle checked label="Primary custody" onChange={() => {}} /></Frame>;

/** Off. */
export const Off = () => <Frame><Toggle checked={false} label="Primary custody" onChange={() => {}} /></Frame>;

/** Without onChange the switch renders display-only: aria-disabled, not focusable. */
export const DisplayOnly = () => <Frame><Toggle checked label="Exception to the $100 minimum order" /></Frame>;

/** Both states on espresso. */
export const DarkTheme = () => (
  <Frame theme="dark">
    <Toggle checked label="Primary custody" onChange={() => {}} />
    <Toggle checked={false} label="Include spousal support" onChange={() => {}} />
  </Frame>
);
