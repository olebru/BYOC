# IRQ-16

**Interrupts**: devices tap the CPU on the shoulder instead of the CPU asking them over and over. It is built from COPRO-16: the same kind of CPU and blitter, with a stack, a keypad, a timer, a VEC register that holds the handler's address and an interrupt controller added, and an instruction set based on COPRO-16's.

## The idea

Without interrupts a program has to **poll**: keep checking whether a key was pressed or the blitter is done, and do little else. With interrupts, a device raises a request, the CPU finishes the instruction it is on, saves where it was, and runs a *handler*. When the handler returns, the interrupted program carries on as if nothing had happened.

## The parts

- [The interrupt controller](exuarch:device/pic) collects requests from up to four devices into pending bits: here [the timer](exuarch:device/tick) on irq0, [the keypad](exuarch:device/keys) on irq1 and [the blitter](exuarch:device/blit) on irq2. It can be switched on and off, and a mask chooses which sources may interrupt.
- [The timer](exuarch:device/tick) raises a request every 20000 ticks, a steady beat for a clock.
- [SP](exuarch:device/sp) runs the stack in [memory](exuarch:device/mem), where the CPU saves its place, and [VEC](exuarch:device/vec) holds the handler's address.

## How an interrupt is taken

The decoder can test a fifth condition, **I**, next to N, V, C and Z: I is 1 when interrupts are enabled and an unmasked request is pending. It is sampled only when an instruction starts, so an instruction always runs to the end.

The [fetch routine](exuarch:instruction/FETCH) has two versions. With I=0 it fetches the next opcode as usual. With I=1 it pushes the PC and the flags on the stack, switches interrupts off and loads the handler's address from VEC into the PC. The handler asks [CAUSE](exuarch:instruction/CAUSE) who called, clears that request with [ACK](exuarch:instruction/ACK), does its work, and ends with [RTI](exuarch:instruction/RTI), which pops the flags and the PC and switches interrupts back on.

## Things to try

1. Run [Three things at once](<exuarch:program/Three things at once>) at ⚡ Max in the [Run view](exuarch:tab/Run). The main loop only counts; the clock, the key counter and the checkerboard all run from the handler.
2. Capture the keyboard on the keypad panel and press keys: each press is one interrupt.
3. Open the [fetch routine](exuarch:instruction/FETCH) in the microcode editor and use the flag preview to switch between I=0 and I=1.
4. Try the timer: change the `TPERI 20000` in the program and see the clock speed up or slow down.
5. Play [Falling blocks](<exuarch:program/Falling blocks>). Set the clock slider to 250 kHz, run it, capture the keyboard on the keypad panel and press space. Left and right move, up turns, down falls faster and space drops.

## Falling blocks

A whole game on the same interrupts. The timer beats every 4000 ticks, 62.5 times a second at 250 kHz, and the handler counts down when the piece next falls and when a held arrow repeats; the main loop reads the keys and moves the piece. The blitter draws each square of the well, a 19 by 19 rectangle, and its interrupt says when it is free for the next.

The game counts time in ticks, so it is timed for one clock speed: at 250 kHz the pieces start at 0.8 seconds a row, and at ⚡ Max they fall as fast as the simulator runs. The well lives in memory at 3500, row by row, next to a copy of what the screen shows, so that after full rows go only the squares that changed are drawn again.

## Read more

- [Interrupts](exuarch:guide/interrupts): the controller, the timer and the I condition.
- [Fetch and the instruction register](exuarch:guide/fetch-and-the-instruction-register): the fetch routine that branches on I.
- [Bus masters and coprocessors](exuarch:guide/bus-masters): the blitter from COPRO-16.
