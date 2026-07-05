window.fairshareStorage = {
    getItem: function (key) {
        return localStorage.getItem(key);
    },
    setItem: function (key, value) {
        localStorage.setItem(key, value);
    },
    removeItem: function (key) {
        localStorage.removeItem(key);
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
