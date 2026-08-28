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
export declare function NavBar({ brand, links, guest, signInLabel }: NavBarProps): import("react").JSX.Element;
