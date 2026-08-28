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
export function MoneyInput({ label, hint, id, "aria-describedby": describedBy, ...rest }: MoneyInputProps) {
  const autoId = useId();
  const inputId = id ?? autoId;
  const hintId = hint ? `${inputId}-hint` : undefined;
  // The hint is real helper text, so screen readers must get it with the field;
  // a caller-provided aria-describedby is kept alongside it.
  const describedByIds = [describedBy, hintId].filter(Boolean).join(" ") || undefined;
  return (
    <div>
      <label className="fs-field-label" htmlFor={inputId}>{label}</label>
      <div className="fs-money">
        <span className="fs-money__prefix">$</span>
        <input
          id={inputId}
          className="fs-money__input"
          inputMode="decimal"
          min={0}
          step={0.01}
          type="number"
          aria-describedby={describedByIds}
          {...rest}
        />
      </div>
      {hint ? <div className="fs-field-hint" id={hintId}>{hint}</div> : null}
    </div>
  );
}
