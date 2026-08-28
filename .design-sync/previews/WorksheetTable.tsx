import { WorksheetTable, WarmProvider } from "@fairshare/warm-counsel";
import type { ReactNode } from "react";

const Frame = ({ theme, children }: { theme?: "light" | "dark"; children: ReactNode }) => (
  <WarmProvider theme={theme}>
    <div style={{ padding: 16 }}>{children}</div>
  </WarmProvider>
);

// Real CS-42 output (engine-true: $3,000/$3,000 gross, one child -> Defendant owes $425),
// so line numbers, labels, and the gold recommended-order row read exactly like the app.
const cs42Rows = [
  { line: "1", label: "MONTHLY GROSS INCOME", plaintiff: "$3,000", defendant: "$3,000", combined: "$6,000" },
  { line: "2", label: "ADJUSTED GROSS INCOME", plaintiff: "$3,000", defendant: "$3,000", combined: "$6,000" },
  { line: "3", label: "PERCENTAGE SHARE OF INCOME", plaintiff: "50%", defendant: "50%", combined: "100%" },
  { line: "4", label: "BASIC CHILD SUPPORT OBLIGATION", combined: "$850" },
  { line: "8", label: "EACH PARENT'S CHILD SUPPORT OBLIGATION", plaintiff: "$425", defendant: "$425" },
  { line: "13", label: "RECOMMENDED CHILD-SUPPORT ORDER", defendant: "$425", highlight: true },
];

/** The line-by-line worksheet, gold highlight on the recommended order. */
export const CS42Result = () => <Frame><WorksheetTable rows={cs42Rows} /></Frame>;

/** Sparse lines render em dashes, never blanks. */
export const SparseLines = () => (
  <Frame>
    <WorksheetTable
      rows={[
        { line: "4", label: "BASIC CHILD SUPPORT OBLIGATION", combined: "$850" },
        { line: "5", label: "WORK-RELATED CHILD-CARE COSTS", plaintiff: "$300", combined: "$300" },
        { line: "13", label: "RECOMMENDED CHILD-SUPPORT ORDER", defendant: "$425", highlight: true },
      ]}
    />
  </Frame>
);

/** The centerpiece on espresso. */
export const DarkTheme = () => <Frame theme="dark"><WorksheetTable rows={cs42Rows} /></Frame>;
