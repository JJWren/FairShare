// Loaded synchronously in <head> so the theme is applied before first paint (no
// light-mode flash). Lives in its own file instead of an inline <script> so the
// CSP can stay at script-src 'self' with no inline allowances.
(function () {
    var stored = localStorage.getItem('fairshare-theme') || 'auto';
    var resolved = stored === 'auto'
        ? (window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light')
        : stored;
    document.documentElement.setAttribute('data-bs-theme', resolved);
    // Warm Counsel tokens key off data-fs-theme (the design-system tokens file is
    // consumed byte-identical, so the app mirrors the attribute rather than forking it).
    document.documentElement.setAttribute('data-fs-theme', resolved);
})();
