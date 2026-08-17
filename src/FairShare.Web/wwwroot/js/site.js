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

window.fairshareTheme = {
    THEME_KEY: 'fairshare-theme',
    apply: function (theme) {
        var resolved = theme === 'auto'
            ? (window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light')
            : theme;
        document.documentElement.setAttribute('data-bs-theme', resolved);
    },
    setTheme: function (theme) {
        localStorage.setItem(this.THEME_KEY, theme);
        this.apply(theme);
    },
    getTheme: function () {
        return localStorage.getItem(this.THEME_KEY) || 'auto';
    }
};
