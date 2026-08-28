export interface ToggleProps {
  /** Current state. */
  checked: boolean;
  /** Visible label, e.g. "Primary custody". */
  label: string;
  /** Change handler; omit for display-only compositions. */
  onChange?: (checked: boolean) => void;
}

/**
 * Labeled switch (teal when on). Used for boolean case facts like primary
 * custody. The whole row is the hit area.
 */
export function Toggle({ checked, label, onChange }: ToggleProps) {
  return (
    <span
      className="fs-toggle"
      role="switch"
      aria-checked={checked}
      tabIndex={0}
      onClick={() => onChange?.(!checked)}
      onKeyDown={(e) => {
        if (e.key === " " || e.key === "Enter") {
          e.preventDefault(); // Space must not scroll the page
          onChange?.(!checked);
        }
      }}
    >
      {label}
      <span className={`fs-toggle__track${checked ? " fs-toggle__track--on" : ""}`}>
        <span className="fs-toggle__thumb" />
      </span>
    </span>
  );
}
