// Anonymous usage statistics through Umami (cloud.umami.is). Umami sets no cookies and stores nothing in the browser;
// it counts unique visitors with a hash that changes every day, and keeps only the country of an address. The script
// only reports from www.exuarch.com (see data-domains in index.html), so local and preview builds send nothing, and
// it honours the browser's Do Not Track setting. When it is blocked or offline, every call here does nothing.
//
// The events come from Analytics.cs. This file adds one summary of the visit, sent once when the page closes: how many
// minutes someone was active (a minute with a key press, click, scroll or typing), and what Analytics.cs has put in it.
(function () {
    var summary = {};
    var activeMinutes = {};
    var sent = false;

    function track(name, data) {
        try {
            if (window.umami && typeof window.umami.track === 'function') window.umami.track(name, data || undefined);
        } catch (e) { }
    }

    var lastInput = Date.now();
    function active() { lastInput = Date.now(); activeMinutes[Math.floor(lastInput / 60000)] = true; }
    ['pointerdown', 'keydown', 'wheel', 'click', 'input'].forEach(function (type) { window.addEventListener(type, active, { passive: true, capture: true }); });

    function minutesBucket(n) {
        if (n <= 1) return '0 to 1';
        if (n <= 5) return '1 to 5';
        if (n <= 15) return '5 to 15';
        if (n <= 60) return '15 to 60';
        return '60 and more';
    }

    // pagehide fires when the tab is closed or navigated away, and Umami sends with keepalive, so the request outlives
    // the page. A browser that discards a background tab without closing it sends nothing.
    window.addEventListener('pagehide', function () {
        if (sent) return;
        sent = true;
        var data = { 'active minutes': minutesBucket(Object.keys(activeMinutes).length) };
        for (var key in summary) data[key] = summary[key];
        track('visit', data);
    });

    window.exuarchAnalytics = {
        track: track,
        summary: function (key, value) { summary[key] = value; },
        // Seconds since the last key press, click or scroll anywhere on the page.
        idleSeconds: function () { return (Date.now() - lastInput) / 1000; }
    };
})();
