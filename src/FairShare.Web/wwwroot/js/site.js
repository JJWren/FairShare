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
