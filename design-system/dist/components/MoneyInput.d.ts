import type { InputHTMLAttributes } from "react";
export interface MoneyInputProps extends Omit<InputHTMLAttributes<HTMLInputElement>, "size" | "type"> {
    /** Visible field label, e.g. "Monthly gross income". */
    label: string;
    /** Optional helper line under the field, e.g. "Before taxes — wages, salary, tips." */
    hint?: string;
}
/**
 * Labeled dollar-amount field: a $ prefix, a 44px-tall numeric input
 * (inputMode decimal), and an optional hint line. Used for every money figure
 * on the worksheets.
 */
export declare function MoneyInput({ label, hint, id, ...rest }: MoneyInputProps): import("react").JSX.Element;
