# Ground zero

A CPU does nothing on its own. Every device waits for a wire to tell it to act, and the decoder drives those wires. In this tutorial you wipe a machine's microcode down to nothing and watch what is left: a counter, counting.

This is the first of four short tutorials that build the core of a CPU one piece at a time. Keep the machine open; each one carries on from the last.

## 1. Make a machine

1. Click **New…** at the top, pick **Minimal CPU**, give it a name and click **Create**.
2. Open [Hardware design](<exuarch:tab/Hardware design>).

You see five devices and the decoder:

- `pc`, a register used as the program counter.
- `mem`, the memory, with its own address register, the MAR.
- `ir`, the instruction register: the decoder's step counter.
- `status`, the flags. Nothing changes it in these tutorials.
- `clk`, the clock.

Between them they have 17 control lines. Click a device to see its lines in the inspector.

## 2. Picture the chips

ExµArch behaves like real chips on a breadboard, so picture them:

- A **register** has two control pins. *Output enable* connects it to the bus wires. *Load* makes it capture the bus on the next clock edge. `pc.output` and `pc.load` are those pins.
- The **decoder** is a ROM, a lookup table in a chip. Put a number on its address pins and the word stored there appears on its data pins. Each data pin is wired to one control pin in the machine, so one word is one bit for each of the 17 lines. That word is a **micro step**.
- **`ir`** is a counter chip, wired to the ROM's address pins. On every clock edge it either clears to 0 (`ir.reset`), loads the bus (`ir.load`), or counts up by one. If both lines are on, clearing wins.

The loop to remember: the counter picks a ROM word, and that word sets the counter's own pins for the next edge.

## 3. Clear the microcode

1. Open [Microcode](exuarch:tab/Microcode).
2. Select `NOP` and click **Delete**. Do the same with `JMP` and `HLT`.
3. Select `FETCH`. Delete its step 2 with ✕.
4. Remove both signals from step 1 with their ×.

The ROM now holds one word, at address 0, with every bit off. The meter in the toolbar reads 1 of 65536 addresses.

## 4. Clear the program

The program uses `NOP` and `HLT`, which are gone.

1. Open [Program](exuarch:tab/Program) and delete the lines `NOP` and `HLT`. The comment can stay.

## 5. Watch the counter count

1. Open [Run](exuarch:tab/Run), and below the machine open the **Decoder ROM** tab.
2. Press **Tick** (→) five times and watch the bits of the micro step register.

`ir` counts 1, 2, 3, 4, 5. No signal asked it to: clear and load are both off, so on each clock edge the counter does the only other thing it can, count up.

## 6. Read the trace

1. Open the **Trace** tab.

Each row is one tick, newest on top. Every row shows `ir 0000→0001`, `ir 0001→0002` and so on, and nothing else changes. Only the oldest row has a step name, `FETCH.1`: the other addresses hold nothing, so every line is off.

The decoder does not know what an instruction is, or that memory exists. Every tick it reads one word and turns on the lines that word says. Right now every word is empty.

2. Press **⟲ Reset** to put `ir` back to 0.

## Next

- [Fetch](exuarch:guide/fetch-routine): put the first two words in the ROM, so the machine reads its program.
- [Devices and control lines](exuarch:guide/devices-and-control-lines) covers what each line does.
