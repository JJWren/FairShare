import type { ReactNode } from "react";
export interface PartyCardProps {
    /** Which side of the case; sets the edge color and the header badge. */
    party: "plaintiff" | "defendant";
    /** Right side of the header - typically a Toggle (custody) or a summary line. */
    headerRight?: ReactNode;
    /** Card body - usually a stack of MoneyInput fields. */
    children?: ReactNode;
}
/**
 * A parent's worksheet-entry card: colored left edge, header with the party
 * badge, and a body that stacks the party's money fields. The two cards sit
 * side by side on desktop and stack on mobile.
 */
export declare function PartyCard({ party, headerRight, children }: PartyCardProps): import("react").JSX.Element;
