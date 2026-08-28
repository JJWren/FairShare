import { NavBar, WarmProvider } from "@fairshare/warm-counsel";
import type { ReactNode } from "react";

const Frame = ({ theme, children }: { theme?: "light" | "dark"; children: ReactNode }) => (
  <WarmProvider theme={theme}>
    <div style={{ padding: 12 }}>{children}</div>
  </WarmProvider>
);

// The app's real primary nav.
const links = [
  { label: "Calculator", href: "/", active: true },
  { label: "Guides", href: "/guides/alabama-cs42" },
  { label: "Scenarios", href: "/scenarios" },
];

/** Guest visit: wordmark, links, the Guest chip, and the terracotta sign-in pill. */
export const Guest = () => <Frame><NavBar links={links} guest /></Frame>;

/** Signed in: the chip drops away. */
export const SignedIn = () => <Frame><NavBar links={links} signInLabel="Account" /></Frame>;

/** Mobile composition: brand + sign-in only (links collapse behind the app's menu). */
export const MobileBar = () => (
  <Frame>
    <div style={{ maxWidth: 390 }}><NavBar guest /></div>
  </Frame>
);

/** The bar on espresso. */
export const DarkTheme = () => <Frame theme="dark"><NavBar links={links} guest /></Frame>;
