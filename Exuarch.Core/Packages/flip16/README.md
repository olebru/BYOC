# FLIP-16

[GPU-16](exuarch:package/GPU-16) with one change: its screen is **double buffered**. Run GPU-16's spinning cube and you watch every frame happen: the screen goes black, then the faces fill in row by row. Here the same frames are drawn out of sight, and each one appears whole. This is how almost every animated display works, and the trick is called double buffering, or page flipping.

## The idea

[The screen](exuarch:device/fb) is a [`doubleFramebuffer`](exuarch:reference/doubleFramebuffer): it holds two pictures. The **front buffer** is the one on the screen. The **back buffer** is the one that gets drawn: every `plot` from the rasterizer, and `clear`, work on it. One control line, `swap`, exchanges the two in a single tick, so the frame that was being drawn is shown, and drawing goes on in the other buffer.

The only new instruction is [SWAP](exuarch:instruction/SWAP), one step with `fb.swap`. It is last in the instruction list, so every other instruction has the same opcode as in GPU-16, and GPU-16's programs run here too: without a `SWAP` they draw, but never show what they drew.

## How a frame is drawn

The cube's loop is GPU-16's with one line added:

```asm
           CALL   wait          ; the rasterizer has finished the frame
           SWAP                 ; show it
           CLS                  ; clear the new back buffer
           ZCLR
           CALL   build         ; the next frame's triangles
           ...
           GO
```

[CLS](exuarch:instruction/CLS) now clears the back buffer, not the screen. It has to: after a swap, the back buffer still holds the frame before last. A program that drew over only part of it would show pieces of an old frame.

The rule from GPU-16 still holds: wait until the rasterizer is idle before writing the triangle list (see GPU-16's README). It now also decides when to swap. A swap while the rasterizer is still drawing would show a half drawn frame, which is what double buffering is there to prevent.

## What real hardware adds

Here a swap happens at once. A real screen is drawn by scanning it top to bottom many times a second. Swap halfway through a scan and the top of the screen shows one frame and the bottom the next, a line called **tearing**. Real displays therefore wait to swap until the scan reaches the bottom, the vertical blank, and that wait is what *vsync* means in game settings. FLIP-16 has no scan, so it cannot tear.

## Things to try

1. Step [A triangle drawn out of sight](<exuarch:program/A triangle drawn out of sight>). The trace shows the rasterizer plotting every pixel, the screen stays black, and then `SWAP` shows the whole triangle in one tick. The screen's header says which buffer is shown.
2. Run [A spinning cube](<exuarch:program/A spinning cube>) at **⚡ Max**, then the same program in GPU-16, and compare.
3. Take the `CLS` out of the cube. The new frame is drawn over the one from two frames ago, and the old faces show through where the cube has moved away.
4. Move the `SWAP` to straight after `GO`, so it swaps while the rasterizer is drawing. Work out what each buffer holds after the swap, then run it and check.

## Read more

- [Bus masters and coprocessors](exuarch:guide/bus-masters): the framebuffer, the double buffered one, and sharing a bus with the rasterizer.
- [The graphics pipeline](exuarch:guide/graphics-pipeline): triangles, shading, depth and projection.
