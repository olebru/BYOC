# DSP-16

[RISC-16](exuarch:package/RISC-16) with one part more: a hardware multiplier. That is what makes a digital signal processor: filters, audio and graphics are mostly sums of products, so a DSP multiplies and adds in one go where an ordinary CPU needs a loop. Here it is enough to draw the Mandelbrot set in colour.

## The multiplier

The [MULTIPLIER](exuarch:device/mac) takes two signed 16 bit numbers, multiplies them into a 32 bit product, and gives back the product shifted right by 8, clamped to 16 bits. That makes it a multiplier for **8.8 fixed point** numbers: 8 bits before the binary point and 8 after, so 256 is 1.0, 384 is 1.5 and -512 is -2.0. A product of two 8.8 numbers has 16 bits after the point, and the shift by 8 brings it back to 8.

[MUL](exuarch:instruction/MUL) `Rd, Rs, Rt` is RISC-16's three register form: it loads Rs and Rt into the multiplier, multiplies, and puts the result in Rd, in 12 ticks with the fetch, as long as an `ADD`. Without it, RISC-16 has to multiply with a loop of shifts and adds, one round for each of the 16 bits, which takes many times as long.

## The Mandelbrot set

Every point c of the picture, with cx across and cy down, runs the same sum over and over: z = z² + c, starting from z = c. Written out for x and y, that is x' = x² - y² + cx and y' = 2xy + cy: three multiplications every step. Some points stay close to 0 for ever; they are the Mandelbrot set, and they are black. The rest sooner or later get further than 2 from 0, which is when x² + y² passes 4, and they are coloured by how soon that happened, from deep blue for the ones that leave at once, through white and orange, to dark brown for those that only just got away. The program gives every point 32 steps.

The picture spans -2.5 to 1.25 across and -1.40625 to 1.40625 down. A point's cx is -2.5 plus its column times the step between points, and that is a MUL too: the step times 256, times the column, shifted right by 8.

The program works out one row of points at a time into memory, then draws it. The screen's cursor moves right after every `PLOT` and wraps at the end of a row of pixels, so the picture fills the screen in order without a single `PX` or `PY`.

## Three pictures

The three programs are the same but for their sizes:

| Program | Points | Each point | Ticks |
|---|---|---|---|
| [Mandelbrot, 80 x 60](<exuarch:program/Mandelbrot, 80 x 60>) | 4,800 | 8 x 8 pixels | 8,141,020 ticks |
| [Mandelbrot, 160 x 120](<exuarch:program/Mandelbrot, 160 x 120>) | 19,200 | 4 x 4 pixels | 25,140,404 ticks |
| [Mandelbrot, 640 x 480](<exuarch:program/Mandelbrot, 640 x 480>) | 307,200 | 1 pixel | 345,850,782 ticks |

Run them with **Max** speed in [Run](exuarch:tab/Run); the clock panel shows how many ticks a second your browser manages. At 20,000 a second the first picture takes about 7 minutes, the second about 21, and the full screen nearly 5 hours. The points inside the set are the slow ones: they always run all 32 steps.

## Things to try

1. Run the 80 x 60 picture with **Max** speed and watch it fill in a row of blocks at a time. Then step through one point with **Instruction** and follow x and y through the multiplier.
2. Zoom in: a smaller step and another start show a part of the edge in more detail. Before long the picture turns blocky, because 8.8 fixed point can not tell apart points closer than 1/256. That is the price of a 16 bit number.
3. Give it more steps: change `MOVI R6, 32` and lengthen the palette to match. The edge gets finer, and the set's inside slower.
4. Draw a Julia set instead: keep c the same for every point, such as cx = -0.8 and cy = 0.156, and start z at the point.
5. Take MUL out and write the multiplication with `LSL`, `LSR` and `ADD` on [RISC-16](exuarch:package/RISC-16), then count the ticks.

## Read more

- [RISC-16](exuarch:package/RISC-16), the same machine without the multiplier.
- [GPU-16](exuarch:package/GPU-16), whose 3D pipeline uses the same multiplier to turn its cube.
- [Registers and the ALU](exuarch:guide/registers-and-the-alu), and what an ALU can not do in one go.
- [ExµArch and real hardware](exuarch:guide/real-hardware): a multiplier that answers in a tick is one of the simplifications.
