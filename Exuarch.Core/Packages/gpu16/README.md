# GPU-16

A CPU with a small **3D graphics pipeline**, built the way the first 3D accelerators were: the CPU does the geometry, and a separate chip fills triangles. Every stage is visible in the Run view, one bus transfer per tick.

## The idea

Drawing a 3D scene has two halves. **Geometry** turns the corners of each shape in space and projects them onto the screen: a few multiplications and a division per corner. **Rasterizing** fills every triangle pixel by pixel: cheap work, but a great deal of it. GPU-16 gives each half its own hardware. The CPU gets a [multiply-accumulate unit](exuarch:device/mac) for the geometry, and [the rasterizer](exuarch:device/rast) fills the triangles by itself on buses of its own, while the CPU carries on.

## The parts

- On the **main** bus: an accumulator CPU with [A](exuarch:device/a), [B](exuarch:device/b), [the ALU](exuarch:device/alu), [memory](exuarch:device/mem), [a stack pointer](exuarch:device/sp) and [the LCD](exuarch:device/lcd), plus [the multiply-accumulate unit](exuarch:device/mac), which works in 8.8 fixed point: 256 is 1.0.
- On the **list** bus: [the triangle list](exuarch:device/lmem), a memory the rasterizer reads triangles from. The CPU reaches it through [the bridge](exuarch:device/bridge).
- On the **video** bus: [the screen](exuarch:device/fb) and [the depth buffer](exuarch:device/zb), which holds how near the nearest thing drawn at each pixel is.
- [The rasterizer](exuarch:device/rast) sits on all three: it takes its orders on main, reads triangles on list and draws on video.

## How a frame is drawn

A triangle is 12 words in the list: x, y, depth and an RGB565 colour for each corner. The CPU writes them with [STL](exuarch:instruction/STL), which crosses the bridge in four ticks per word. Then it tells the rasterizer where the triangles start with [GADR](exuarch:instruction/GADR) and how many there are with [GCNT](exuarch:instruction/GCNT), and starts it with [GO](exuarch:instruction/GO).

The rasterizer reads each triangle, two ticks per word, then fills it row by row. For every pixel it reads the depth buffer, and only if the triangle is nearer there does it write the new depth and plot the colour, blended from the three corner colours. [GST](exuarch:instruction/GST) tells the CPU whether it is still busy.

For 3D, the CPU first turns each corner with [MA](exuarch:instruction/MA), [MB](exuarch:instruction/MB), [MUL](exuarch:instruction/MUL) and [MAC](exuarch:instruction/MAC): x times the cosine plus z times the sine. It then projects the result with [MDIV](exuarch:instruction/MDIV): x times the focal length, divided by the distance. [MRD](exuarch:instruction/MRD) reads the answer back.

## Things to try

1. [One flat triangle](<exuarch:program/One flat triangle>): step it and watch the list bus carry the 12 words, then the video bus fill the rows.
2. [A shaded triangle](<exuarch:program/A shaded triangle>): three corner colours blended across it.
3. [Two triangles through each other](<exuarch:program/Two triangles through each other>): each is nearer on one side, so the depth buffer cuts them along a line neither triangle has. Show the depth buffer next to the screen in the Run view.
4. [A pinwheel from one list](<exuarch:program/A pinwheel from one list>): sixteen triangles and one `GO`, and the CPU prints dots on the LCD all the while.
5. [A spinning cube](<exuarch:program/A spinning cube>): the whole pipeline. Run it at full speed with **⚡ Max**; while the rasterizer draws one frame, the CPU is already turning the corners for the next. A frame is about 150,000 ticks, almost all of them pixels, so in the browser it takes a few seconds: you see each frame being drawn.
6. Make the cube faster with **back-face culling**. A face turned away from the camera has its corners in the opposite turning order on the screen to a face turned towards it, so it can be left out of the list. Two multiplications per triangle on the CPU save the rasterizer every pixel of the back faces.

## Read more

- [The graphics pipeline](exuarch:guide/graphics-pipeline): triangles, shading, depth and projection, and what they cost.
- [Bus masters and coprocessors](exuarch:guide/bus-masters): how a device drives a bus by itself.
- [Bridges between buses](exuarch:guide/bridges): how the CPU reaches the triangle list.
