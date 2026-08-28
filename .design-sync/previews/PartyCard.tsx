import { MoneyInput, PartyCard, Toggle, WarmProvider } from "@fairshare/warm-counsel";
import type { ReactNode } from "react";

const Frame = ({ theme, children }: { theme?: "light" | "dark"; children: ReactNode }) => (
  <WarmProvider theme={theme}>
    <div style={{ padding: 16, maxWidth: 420 }}>{children}</div>
  </WarmProvider>
);

const plaintiffFields = (
  <div style={{ display: "grid", gap: 14 }}>
    <MoneyInput label="Monthly gross income" placeholder="0" defaultValue="3000" />
    <MoneyInput
      label="Work-related child-care costs"
      hint="Costs due to employment or job search, for the children of this case."
      placeholder="0"
    />
  </div>
);

/** The worksheet-entry card: teal edge, badge header, custody toggle, money fields. */
export const Plaintiff = () => (
  <Frame>
    <PartyCard party="plaintiff" headerRight={<Toggle checked label="Primary custody" onChange={() => {}} />}>
      {plaintiffFields}
    </PartyCard>
  </Frame>
);

/** The defendant twin - violet edge, same anatomy. */
export const Defendant = () => (
  <Frame>
    <PartyCard party="defendant" headerRight={<Toggle checked={false} label="Primary custody" onChange={() => {}} />}>
      <div style={{ display: "grid", gap: 14 }}>
        <MoneyInput label="Monthly gross income" placeholder="0" defaultValue="3000" />
        <MoneyInput
          label="Health-care coverage costs"
          hint="The children's share of the premium only."
          placeholder="0"
        />
      </div>
    </PartyCard>
  </Frame>
);

/** The plaintiff card on espresso. */
export const DarkTheme = () => (
  <Frame theme="dark">
    <PartyCard party="plaintiff" headerRight={<Toggle checked label="Primary custody" onChange={() => {}} />}>
      {plaintiffFields}
    </PartyCard>
  </Frame>
);
