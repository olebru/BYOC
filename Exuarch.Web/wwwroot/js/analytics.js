// Anonymous usage statistics through Umami (cloud.umami.is). Umami sets no cookies and stores nothing in the browser;
// it counts unique visitors with a hash that changes every day, and keeps only the country of an address. The script
// only reports from www.exuarch.com (see data-domains in index.html), so local and preview builds send nothing, and
// it honours the browser's Do Not Track setting. When it is blocked or offline, every call here does nothing.
window.exuarchAnalytics = {
    track: function (name, data) {
        try {
            if (window.umami && typeof window.umami.track === 'function') window.umami.track(name, data || undefined);
        } catch (e) { }
    },
    // How long the app took to start, in seconds since the page began loading, bucketed.
    loadSeconds: function () {
        try { return performance.now() / 1000; } catch (e) { return -1; }
    }
};
