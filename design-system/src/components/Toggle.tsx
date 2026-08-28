export interface ToggleProps {
  /** Current state. */
  checked: boolean;
  /** Visible label, e.g. "Primary custody". */
  label: string;
  /** Change handler; omitting it renders the switch disabled (display-only). */
  onChange?: (checked: boolean) => void;
}

/**
 * Labeled switch (teal when on). Used for boolean case facts like primary
 * custody. The whole row is the hit area. Without onChange it renders as a
 * disabled switch - not focusable, aria-disabled - never an operable-looking
 * control that ignores input.
 */
export function Toggle({ checked, label, onChange }: ToggleProps) {
  const interactive = typeof onChange === "function";
  return (
    <span
      className="fs-toggle"
      role="switch"
      aria-checked={checked}
      aria-disabled={interactive ? undefined : true}
      tabIndex={interactive ? 0 : undefined}
      onClick={interactive ? () => onChange(!checked) : undefined}
      onKeyDown={
        interactive
          ? (e) => {
              if (e.key === " " || e.key === "Enter") {
                e.preventDefault(); // Space must not scroll the page
                onChange(!checked);
              }
            }
          : undefined
      }
    >
      {label}
      <span className={`fs-toggle__track${checked ? " fs-toggle__track--on" : ""}`}>
        <span className="fs-toggle__thumb" />
      </span>
    </span>
  );
}
