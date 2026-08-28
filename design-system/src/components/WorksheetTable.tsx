export interface WorksheetRow {
  /** The official form line number, e.g. "1", "1a", "13". */
  line: string;
  /** The line's label as printed on the form. */
  label: string;
  plaintiff?: string;
  defendant?: string;
  combined?: string;
  /** True on the recommended-order line - renders the gold highlight. */
  highlight?: boolean;
}

export interface WorksheetTableProps {
  /** Worksheet lines in form order. */
  rows: WorksheetRow[];
}

/**
 * The line-by-line results table - FairShare's centerpiece. Line numbers match
 * the court's own form; the recommended-order row gets the gold highlight.
 * Empty cells render an em dash.
 */
export function WorksheetTable({ rows }: WorksheetTableProps) {
  return (
    <table className="fs-worksheet">
      <thead>
        <tr>
          <th scope="col">#</th>
          <th scope="col">Item</th>
          <th scope="col" className="fs-worksheet--num">Plaintiff</th>
          <th scope="col" className="fs-worksheet--num">Defendant</th>
          <th scope="col" className="fs-worksheet--num">Combined</th>
        </tr>
      </thead>
      <tbody>
        {rows.map((row) => (
          <tr key={row.line} className={row.highlight ? "fs-worksheet__row--gold" : undefined}>
            <td className="fs-worksheet__line">{row.line}</td>
            <td>{row.label}</td>
            <td className="fs-worksheet--num">{row.plaintiff ?? "—"}</td>
            <td className="fs-worksheet--num">{row.defendant ?? "—"}</td>
            <td className="fs-worksheet--num">{row.combined ?? "—"}</td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}
