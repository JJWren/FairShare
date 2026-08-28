// Hands the browser a file the SPA received over XHR (Blazor passes byte[] as a Uint8Array).
window.fairshareDownload = {
    saveFile: function (fileName, contentType, bytes) {
        var blob = new Blob([bytes], { type: contentType });
        var url = URL.createObjectURL(blob);
        var link = document.createElement('a');
        link.href = url;
        link.download = fileName;
        document.body.appendChild(link);
        link.click();
        link.remove();
        setTimeout(function () { URL.revokeObjectURL(url); }, 1000);
    }
};

// First-party analytics helpers: browser privacy signals and the first-load referrer.
// No cookies, no identifiers, nothing stored client-side (ADR 0003).
window.fairshareAnalytics = {
    isOptedOut: function () {
        return navigator.doNotTrack === '1'
            || window.doNotTrack === '1'
            || navigator.globalPrivacyControl === true;
    },
    referrer: function () {
        return document.referrer || null;
    }
};

window.fairshareTheme = {
    THEME_KEY: 'fairshare-theme',
    apply: function (theme) {
        var resolved = theme === 'auto'
            ? (window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light')
            : theme;
        document.documentElement.setAttribute('data-bs-theme', resolved);
        // Mirror for the Warm Counsel tokens (same reasoning as theme-init.js).
        document.documentElement.setAttribute('data-fs-theme', resolved);
    },
    setTheme: function (theme) {
        localStorage.setItem(this.THEME_KEY, theme);
        this.apply(theme);
    },
    getTheme: function () {
        return localStorage.getItem(this.THEME_KEY) || 'auto';
    }
};

// Snapshot hygiene (#173): a page served from /_snapshot arrives with the previous
// render's runtime-appended head tags baked in - the route canonical from Blazor's
// HeadContent and the JSON-LD injected below. Drop them before this file re-injects and
// before Blazor boots and appends fresh ones; otherwise every snapshot load carries
// duplicates, and a client-side navigation leaves the snapshot's stale canonical as the
// first (winning) one. Crawlers that don't run JS never execute this and keep the baked
// tags - which is the whole point of the snapshot.
(function () {
    document.querySelectorAll('head link[rel="canonical"], head script[type="application/ld+json"]')
        .forEach(function (el) { el.remove(); });
})();

// Structured data for crawlers. Injected from this external file instead of an inline
// <script type="application/ld+json"> so the CSP's script-src 'self' is unambiguously
// satisfied; every crawler that matters here renders JS anyway (the whole site is a SPA).
(function () {
    var data = {
        "@context": "https://schema.org",
        "@type": "WebApplication",
        "name": "FairShare",
        "url": "https://easychildsupport.fyi/",
        "applicationCategory": "FinanceApplication",
        "operatingSystem": "Web",
        "description": "FairShare is a free child-support calculator: transparent, line-by-line estimates under your state's official guidelines, matching the court's own worksheets.",
        "offers": { "@type": "Offer", "price": "0", "priceCurrency": "USD" }
    };
    var script = document.createElement('script');
    script.type = 'application/ld+json';
    script.textContent = JSON.stringify(data);
    document.head.appendChild(script);
})();

// Desktop hover-open for navbar dropdowns (Guides, Admin - #225/#226), driven through
// Bootstrap's Dropdown API so the .show state and the toggle's aria-expanded stay in
// sync (a pure CSS :hover display would open the menu while aria-expanded said false).
// Delegated on document because Blazor renders the navbar after this file runs. Touch
// and keyboard use Bootstrap's click toggle unchanged; inside the collapsed mobile menu
// these handlers never fire (the media checks fail below lg).
(function () {
    function hoverCapable() {
        return window.bootstrap &&
            window.matchMedia('(hover: hover) and (pointer: fine)').matches &&
            window.matchMedia('(min-width: 992px)').matches;
    }
    function toggleOf(e) {
        var li = e.target instanceof Element && e.target.closest('.navbar .nav-item.dropdown');
        // Moves within the item (toggle <-> its own menu) are not enter/leave events.
        if (!li || (e.relatedTarget instanceof Element && li.contains(e.relatedTarget))) return null;
        return li.querySelector('[data-bs-toggle="dropdown"]');
    }
    document.addEventListener('pointerover', function (e) {
        if (!hoverCapable()) return;
        var t = toggleOf(e);
        if (t) window.bootstrap.Dropdown.getOrCreateInstance(t).show();
    });
    document.addEventListener('pointerout', function (e) {
        if (!hoverCapable()) return;
        var t = toggleOf(e);
        if (t) window.bootstrap.Dropdown.getOrCreateInstance(t).hide();
    });
})();
