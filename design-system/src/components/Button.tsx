import type { ButtonHTMLAttributes } from "react";

export interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  /** "primary" (filled terracotta) for the one main action; "outline-danger" for Reset-style destructive actions; "outline-accent" for secondary actions like Export. */
  variant?: "primary" | "outline-danger" | "outline-accent";
}

/**
 * Pill button. One primary per view; outline variants for secondary and
 * destructive actions. Minimum hit target 44px is built in.
 */
export function Button({ variant = "primary", className, ...rest }: ButtonProps) {
  const variantClass =
    variant === "outline-danger" ? " fs-btn--outline-danger" :
    variant === "outline-accent" ? " fs-btn--outline-accent" : "";
  return <button type="button" className={`fs-btn${variantClass}${className ? ` ${className}` : ""}`} {...rest} />;
}
