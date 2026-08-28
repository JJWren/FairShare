import type { ReactNode } from "react";
export interface PartyBadgeProps {
    /** Which side of the case: plaintiff renders teal, defendant renders violet. */
    party: "plaintiff" | "defendant";
    /** Custom label; defaults to the capitalized party name. */
    children?: ReactNode;
}
/**
 * The party identity chip. FairShare's core convention: plaintiff is always
 * teal, defendant always violet, and color is never the only signal - the
 * badge text carries the meaning too. Both pairs are WCAG AA in both themes.
 */
export declare function PartyBadge({ party, children }: PartyBadgeProps): import("react").JSX.Element;
