// Draws framebuffer regions sent from .NET onto a canvas. .NET sends the raw RGB565 words (two little endian bytes
// per pixel); expanding them to RGBA here is far faster than doing it in interpreted .NET.
window.byocScreen = {
    draw: function (canvasId, x, y, width, height, rgb565) {
        const canvas = document.getElementById(canvasId);
        if (!canvas) return;
        const count = width * height;
        const rgba = new Uint8ClampedArray(count * 4);
        for (let i = 0, j = 0; i < count; i++, j += 4) {
            const p = rgb565[2 * i] | (rgb565[2 * i + 1] << 8);
            const r = (p >> 11) & 0x1F, g = (p >> 5) & 0x3F, b = p & 0x1F;
            rgba[j] = (r << 3) | (r >> 2);
            rgba[j + 1] = (g << 2) | (g >> 4);
            rgba[j + 2] = (b << 3) | (b >> 2);
            rgba[j + 3] = 255;
        }
        canvas.getContext('2d').putImageData(new ImageData(rgba, width, height), x, y);
    },
};
