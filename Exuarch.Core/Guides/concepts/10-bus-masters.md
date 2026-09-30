# Bus masters and coprocessors

In most of a machine, nothing happens unless the microcode says so. Every control line is switched on by a step, and the decoder is the only thing issuing steps. A bus master breaks that rule. It is a device that drives another device's control lines by itself, tick after tick, while the CPU carries on with its own program. In ExµArch that device is the blitter, a small graphics coprocessor.

## The framebuffer it draws on

A [`framebuffer`](exuarch:reference/framebuffer) is a 640 × 480 colour screen. Each pixel is one 16 bit RGB565 word: 5 bits of red, 6 of green and 5 of blue, so `0xF800` is red, `0x07E0` green and `0x001F` blue. It has a cursor and four control lines:

- `loadx` sets the cursor column from the bus. The value wraps at 640.
- `loady` sets the cursor row from the bus. The value wraps at 480.
- `plot` writes the bus value at the cursor and moves one pixel right. Past the last column it goes to the start of the next row, and past the last row back to the top.
- `clear` blanks the screen and puts the cursor at the top left.

A CPU can drive these lines from its own microcode, as BYOC-16 does. Filling a rectangle that way costs at least one tick per pixel, and the CPU can do nothing else meanwhile.

## The blitter

A [`blitter`](exuarch:reference/blitter) has two bus ports, `host` and `video`, and one connection, `screen`, which must name a framebuffer. Put the host port on the CPU's bus, and put the video port and the framebuffer on a bus of their own. Its control lines all face the host side:

- `loadx`, `loady`, `loadw` and `loadh` take the rectangle's left column, top row, width and height from the host bus.
- `loadcolour` takes the RGB565 fill colour from the host bus.
- `start` starts the job, if the width and height are both more than 0.
- `status` puts 1 on the host bus while the blitter is busy and 0 when it is idle.

After `start`, the blitter works on its own. Every tick, in the drive half, it puts one value on the video bus and switches on one of the screen's lines, just as microcode would:

1. the rectangle's column, with `loadx`;
2. the current row, with `loady`;
3. the colour, with `plot`, once for each pixel across the width.

Then it moves to the next row. A row takes its width plus two ticks, so a 200 × 150 rectangle takes 150 × 202 ticks. There is no clipping: a rectangle that runs past the right edge wraps, as the cursor does.

The blitter reads its rectangle and colour as it goes, so wait until `status` reads 0 before you load the next job.

## Two buses, two things at once

The point of the separate video bus is that the blitter's transfers never meet the CPU's. Each bus carries one value per tick, and a second device driving it in the same tick stops the machine with a bus error. With the framebuffer alone on the video bus, the blitter has that bus to itself. The CPU keeps fetching and running instructions on the main bus in the same ticks.

In the Run view you can see both. The **Last tick** panel lists each bus with the device that drove it and the devices that read it. On the video bus the driver is the blitter and the reader is the screen, even though no microcode step named them.

## COPRO-16

[COPRO-16](exuarch:package/COPRO-16) is built around this idea. The accumulator CPU lives on `main` with its memory and the LCD. The blitter sits between `main` and `video`, and the framebuffer is on `video`. The instruction set gives the CPU one instruction per blitter line:

```asm
        BXI    80          ; left column
        BYI    80          ; top row
        BWI    200         ; width
        BHI    150         ; height
        BCI    0xF800      ; red
        GO                 ; blit.start: the blitter takes over
wait:   BST                ; A = 1 while busy
        ...
```

`BXI` through `BCI` read their operand from program memory and switch on `blit.loadx` through `blit.loadcolour` with it on the bus. `GO` is a single step with `blit.start`. `BST` puts `blit.status` into A. Between `GO` and the moment `BST` reads 0, the program is free to do other work, such as printing on the LCD.

## Finishing with an interrupt

Polling `status` works, but the blitter can also tell the CPU when it is done. It raises an interrupt request each time a job finishes. Connect it to one of an interrupt controller's sources, and the CPU is interrupted instead of having to ask. [IRQ-16](exuarch:package/IRQ-16) wires it to `irq2` this way. See [interrupts](exuarch:guide/interrupts).

## See also

- [Bridges between buses](exuarch:guide/bridges)
- [Interrupts](exuarch:guide/interrupts)
- [blitter reference](exuarch:reference/blitter) and [framebuffer reference](exuarch:reference/framebuffer)
- [COPRO-16](exuarch:package/COPRO-16)
