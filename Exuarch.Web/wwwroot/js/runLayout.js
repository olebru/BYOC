// Resizing the Run view's two columns and the panels below them. A handle is any element with data-resize="left",
// "right" or "bottom". While it is dragged only a CSS variable on the Run view changes, so nothing is re-rendered
// however fast the machine is running; when it is let go, the size is handed to .NET to keep. A double-click on a
// handle puts its size back to the default.
window.exuarchRunLayout = {
    // Tells .NET when the drawing's panel changes width, once per frame at most, so it can fit the machine again.
    observe: function (element, dotnet) {
        if (!element || element.__exuarchObserved || typeof ResizeObserver === 'undefined') return;
        element.__exuarchObserved = true;
        var pending = false;
        new ResizeObserver(function () {
            if (pending) return;
            pending = true;
            requestAnimationFrame(function () {
                pending = false;
                if (element.isConnected) dotnet.invokeMethodAsync('SchematicResized', element.clientWidth);
            });
        }).observe(element);
    },

    attach: function (run, dotnet) {
        if (!run || run.__exuarchLayout) return;
        run.__exuarchLayout = true;

        function limits(kind) {
            var main = run.querySelector('.main');
            if (kind === 'left' || kind === 'right') return { min: 220, max: Math.max(220, main.getBoundingClientRect().width - 360) };
            if (kind === 'split') return { min: 15, max: 85 };
            return { min: 120, max: Math.max(120, run.getBoundingClientRect().height - 260) };
        }

        run.addEventListener('pointerdown', function (e) {
            var handle = e.target.closest && e.target.closest('[data-resize]');
            if (!handle || !run.contains(handle) || e.button !== 0) return;
            e.preventDefault();
            var kind = handle.getAttribute('data-resize');
            var property = kind === 'split' ? '--bottom-split' : '--' + kind + (kind === 'bottom' ? '-height' : '-width');
            // The split is a share of the width below, in percent, and set on the panels below rather than the view.
            var target = kind === 'split' ? handle.parentElement : run;
            var range = limits(kind);
            var size = null;
            try { handle.setPointerCapture(e.pointerId); } catch (err) { }
            handle.classList.add('dragging');
            run.classList.add('resizing');

            function move(ev) {
                var main = run.querySelector('.main').getBoundingClientRect();
                var below = target.getBoundingClientRect();
                var value = kind === 'left' ? ev.clientX - main.left
                    : kind === 'right' ? main.right - ev.clientX
                    : kind === 'split' ? (ev.clientX - below.left) / below.width * 100
                    : run.getBoundingClientRect().bottom - ev.clientY;
                value = Math.min(range.max, Math.max(range.min, value));
                size = kind === 'split' ? Math.round(value * 10) / 10 : Math.round(value);
                target.style.setProperty(property, size + (kind === 'split' ? '%' : 'px'));
            }
            function up() {
                handle.removeEventListener('pointermove', move);
                handle.removeEventListener('pointerup', up);
                handle.removeEventListener('pointercancel', up);
                handle.classList.remove('dragging');
                run.classList.remove('resizing');
                if (size !== null) dotnet.invokeMethodAsync('LayoutResized', kind, size);
            }
            handle.addEventListener('pointermove', move);
            handle.addEventListener('pointerup', up);
            handle.addEventListener('pointercancel', up);
        });

        run.addEventListener('dblclick', function (e) {
            var handle = e.target.closest && e.target.closest('[data-resize]');
            if (handle && run.contains(handle)) dotnet.invokeMethodAsync('LayoutResized', handle.getAttribute('data-resize'), -1);
        });
    }
};

// Shows an element at its actual size when there is room for it, and otherwise first takes the padding of the panel
// around it and then zooms it out as far as it takes to fit, so it never needs a scroll bar. The element is the
// wrapper's only child.
window.exuarchFit = {
    observe: function (wrapper) {
        if (!wrapper || wrapper.__exuarchFit || typeof ResizeObserver === 'undefined') return;
        wrapper.__exuarchFit = true;
        var content = wrapper.firstElementChild;
        var padding = function (e) { var s = getComputedStyle(e); return parseFloat(s.paddingLeft) + parseFloat(s.paddingRight); };
        var host = wrapper.parentElement;
        while (host && host.parentElement && padding(host) === 0) host = host.parentElement;
        if (!content || !host) return;

        function fit() {
            if (!wrapper.isConnected) return;
            content.style.zoom = '';
            wrapper.style.marginLeft = wrapper.style.marginRight = '';
            var natural = content.getBoundingClientRect().width;
            var room = wrapper.clientWidth;
            if (natural === 0 || natural <= room) return;
            var style = getComputedStyle(host);
            var left = parseFloat(style.paddingLeft), right = parseFloat(style.paddingRight);
            wrapper.style.marginLeft = -left + 'px';
            wrapper.style.marginRight = -right + 'px';
            // A pixel to spare for the rounding of the zoomed borders.
            var scale = (room + left + right - 1) / natural;
            if (scale < 1) content.style.zoom = Math.floor(scale * 1000) / 1000;
        }

        var pending = false;
        new ResizeObserver(function () {
            if (pending) return;
            pending = true;
            requestAnimationFrame(function () { pending = false; fit(); });
        }).observe(host);
        if (document.fonts && document.fonts.ready) document.fonts.ready.then(fit);
        fit();
    }
};
