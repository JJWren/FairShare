export interface NavLink {
  label: string;
  href: string;
  /** Marks the current page. */
  active?: boolean;
}

export interface NavBarProps {
  /** Brand text; Lora wordmark. */
  brand?: string;
  /** Primary navigation links, e.g. Calculator / Guides / Scenarios. */
  links?: NavLink[];
  /** Shows the Guest chip when the visitor has no account session. */
  guest?: boolean;
  /** Label for the sign-in pill. */
  signInLabel?: string;
}

/**
 * The app's top bar: Lora wordmark, primary links, guest chip, and the
 * terracotta sign-in pill. On mobile, links collapse behind the app's menu -
 * compose only brand + sign-in there.
 */
export function NavBar({ brand = "FairShare", links = [], guest = false, signInLabel = "Sign in" }: NavBarProps) {
  return (
    <nav className="fs-navbar">
      <span style={{ display: "flex", gap: 36, alignItems: "center" }}>
        <a className="fs-navbar__brand" href="/">{brand}</a>
        {links.length > 0 ? (
          <span className="fs-navbar__links">
            {links.map((link) => (
              <a key={link.label} className={`fs-navbar__link${link.active ? " fs-navbar__link--active" : ""}`} href={link.href}>
                {link.label}
              </a>
            ))}
          </span>
        ) : null}
      </span>
      <span style={{ display: "flex", gap: 14, alignItems: "center" }}>
        {guest ? <span className="fs-navbar__guest">Guest</span> : null}
        <a className="fs-navbar__signin" href="/login">{signInLabel}</a>
      </span>
    </nav>
  );
}
