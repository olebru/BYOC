// Draws framebuffer regions sent from .NET as RGBA bytes onto a canvas.
window.byocScreen = {
    draw: function (canvasId, x, y, width, height, rgba) {
        const canvas = document.getElementById(canvasId);
        if (!canvas) return;
        const image = new ImageData(new Uint8ClampedArray(rgba.buffer, rgba.byteOffset, rgba.byteLength), width, height);
        canvas.getContext('2d').putImageData(image, x, y);
    },
};
