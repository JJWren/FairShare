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
export declare function Toggle({ checked, label, onChange }: ToggleProps): import("react").JSX.Element;
