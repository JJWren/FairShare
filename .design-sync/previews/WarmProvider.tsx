import { Button, MoneyInput, WarmProvider } from "@fairshare/warm-counsel";

const sample = (
  <div style={{ padding: 20, display: "grid", gap: 14, maxWidth: 380 }}>
    <h2 style={{ font: "600 22px var(--fs-font-display)", margin: 0 }}>Alabama CS-42</h2>
    <p style={{ margin: 0 }}>
      Line-by-line estimates under Rule 32 - the same worksheet the court uses.
    </p>
    <MoneyInput label="Monthly gross income" placeholder="0" defaultValue="3000" />
    <div><Button>Calculate</Button></div>
  </div>
);

/** The cream default: page background, ink color, Lora display + Karla body. */
export const Light = () => <WarmProvider>{sample}</WarmProvider>;

/** The espresso theme via theme="dark" - same tokens, re-tuned values. */
export const Dark = () => <WarmProvider theme="dark">{sample}</WarmProvider>;
