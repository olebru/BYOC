// Small helpers for the machine editor. Pointer coordinates are converted to canvas coordinates in C#.
window.byocEditor = {
    rect: function (element) {
        const r = element.getBoundingClientRect();
        return { left: r.left, top: r.top, width: r.width, height: r.height };
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
