import type { ReactNode } from "react";
import { PartyBadge } from "./PartyBadge";

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
export function PartyCard({ party, headerRight, children }: PartyCardProps) {
  return (
    <section className={`fs-party-card fs-party-card--${party}`}>
      <header className="fs-party-card__header">
        <PartyBadge party={party} />
        {headerRight}
      </header>
      <div className="fs-party-card__body">{children}</div>
    </section>
  );
}
