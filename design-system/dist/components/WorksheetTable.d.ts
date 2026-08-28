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
export declare function WorksheetTable({ rows }: WorksheetTableProps): import("react").JSX.Element;
