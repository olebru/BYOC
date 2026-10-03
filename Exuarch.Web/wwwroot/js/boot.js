// Runs first, before anything else on the page loads. Kept out of index.html so the Content Security Policy can
// forbid inline scripts.

// A phone gets a short introduction instead (phone.html), before any of the app is downloaded: the app needs a
// big screen, a keyboard and a mouse. A phone is a touch screen whose shorter side is under 600 CSS pixels, so
// tablets still get the app. "Open it here anyway" on that page comes back with ?full, which is remembered.
(function () {
    var full = new URLSearchParams(location.search).has('full');
    if (full) history.replaceState(null, '', location.pathname + location.hash);
    try {
        if (full) localStorage.setItem('exuarch.full', '1');
        else full = localStorage.getItem('exuarch.full') === '1';
    } catch (e) { }
    var phone = Math.min(screen.width, screen.height) < 600 && matchMedia('(pointer: coarse)').matches;
    if (phone && !full) {
        // Nothing below is needed, so none of it is downloaded. Stopping first, because stop() would also
        // cancel a navigation that had already started.
        window.stop();
        location.replace('phone.html');
    }
})();

try { document.documentElement.dataset.theme = localStorage.getItem('exuarch.theme') === 'light' ? 'light' : 'dark'; }
catch (e) { document.documentElement.dataset.theme = 'dark'; }

// The service worker keeps the app working offline. Registered once the page has loaded, so it does not compete
// with the app's own downloads.
window.addEventListener('load', function () {
    if ('serviceWorker' in navigator) navigator.serviceWorker.register('service-worker.js');
});
