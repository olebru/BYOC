// Small helpers for the machine editor. Pointer coordinates are converted to canvas coordinates in C#.
// Firefox only starts an HTML5 drag when the drag carries data; Blazor handlers keep the real state.
document.addEventListener('dragstart', function (e) {
    if (e.dataTransfer && e.dataTransfer.types.length === 0) e.dataTransfer.setData('text/plain', '');
});

window.exuarchEditor = {
    rect: function (element) {
        const r = element.getBoundingClientRect();
        return { left: r.left, top: r.top, width: r.width, height: r.height };
    },
    scrollToId: function (id) {
        const element = document.getElementById(id);
        if (!element) return;
        // Scroll only the closest scrolling container, so outer panels and the page stay where they are.
        let box = element.parentElement;
        while (box && !(box.scrollHeight > box.clientHeight && /(auto|scroll)/.test(getComputedStyle(box).overflowY))) box = box.parentElement;
        if (!box) return;
        const e = element.getBoundingClientRect(), b = box.getBoundingClientRect();
        if (e.top < b.top) box.scrollTop -= b.top - e.top;
        else if (e.bottom > b.bottom) box.scrollTop += e.bottom - b.bottom;
    },
    focus: function (element) {
        element.focus({ preventScroll: true });
    },
    download: function (fileName, text) {
        const url = URL.createObjectURL(new Blob([text], { type: 'application/json' }));
        const a = document.createElement('a');
        a.href = url;
        a.download = fileName;
        a.click();
        URL.revokeObjectURL(url);
    }
};

// Package READMEs: links into the app (exuarch:...) go to .NET, web links open in a new tab.
window.exuarchReadme = {
    scrollTop: function (element) {
        if (element) element.scrollTop = 0;
    },
    attach: function (element, dotnet) {
        element.addEventListener('click', function (e) {
            const link = e.target.closest('a');
            if (!link || !element.contains(link)) return;
            const href = link.getAttribute('href') || '';
            if (href.startsWith('exuarch:')) {
                e.preventDefault();
                dotnet.invokeMethodAsync('Follow', href);
            } else if (/^(https?:|mailto:)/i.test(href)) {
                link.target = '_blank';
                link.rel = 'noopener noreferrer';
            } else if (href === '#') {
                e.preventDefault();
            }
        });
    },
};

// Whether this browser has been here before; the first call remembers that it has, so the guide opens only once.
window.exuarchWelcome = {
    key: 'exuarch.welcomed',
    seen: function () {
        try {
            if (localStorage.getItem(this.key)) return true;
            localStorage.setItem(this.key, '1');
        } catch { }
        return false;
    },
};

// The workspace in the browser's storage. Every call is safe: private windows, blocked storage or a full quota
// just mean nothing is kept, which set reports by returning false.
window.exuarchStore = {
    get: function (key) {
        try { return localStorage.getItem(key); } catch { return null; }
    },
    set: function (key, value) {
        try { localStorage.setItem(key, value); return true; } catch { return false; }
    },
    remove: function (key) {
        try { localStorage.removeItem(key); } catch { }
    },
};
