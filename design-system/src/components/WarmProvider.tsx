import type { ReactNode } from "react";

export interface WarmProviderProps {
  /** Color theme. "light" is the cream default; "dark" is the espresso theme. */
  theme?: "light" | "dark";
  children?: ReactNode;
}

/**
 * Root wrapper for everything built with Warm Counsel. Applies the token scope
 * (page background, ink color, Karla body type) and selects the theme via
 * data-fs-theme. Wrap the whole screen in exactly one WarmProvider.
 */
export function WarmProvider({ theme = "light", children }: WarmProviderProps) {
  return (
    <div className="fs-root" data-fs-theme={theme === "dark" ? "dark" : undefined}>
      {children}
    </div>
  );
}
