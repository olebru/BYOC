// Small helpers for the machine editor. Pointer coordinates are converted to canvas coordinates in C#.
// Firefox only starts an HTML5 drag when the drag carries data; Blazor handlers keep the real state.
document.addEventListener('dragstart', function (e) {
    if (e.dataTransfer && e.dataTransfer.types.length === 0) e.dataTransfer.setData('text/plain', '');
});

window.byocEditor = {
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
