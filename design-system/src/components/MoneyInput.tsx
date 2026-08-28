import { useId } from "react";
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
export function MoneyInput({ label, hint, id, ...rest }: MoneyInputProps) {
  const autoId = useId();
  const inputId = id ?? autoId;
  return (
    <div>
      <label className="fs-field-label" htmlFor={inputId}>{label}</label>
      <div className="fs-money">
        <span className="fs-money__prefix">$</span>
        <input id={inputId} className="fs-money__input" inputMode="decimal" min={0} step={0.01} type="number" {...rest} />
      </div>
      {hint ? <div className="fs-field-hint">{hint}</div> : null}
    </div>
  );
}
