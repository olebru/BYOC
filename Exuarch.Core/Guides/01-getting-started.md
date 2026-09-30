# Getting started

ExµArch is a workbench for computers that don't exist yet. You wire up the hardware, write the microcode that makes it do things, write a program in the instruction set that microcode defines, and watch it run one clock tick at a time. Nothing is fixed: the number of buses, the registers, the instruction set, even whether there is an instruction set at all.

## The tabs

- [Hardware design](<exuarch:tab/Hardware design>): the devices and the buses between them. Drag a device from the palette onto the canvas and it lands on the nearest bus. Select a device to see what it does, its control lines, its connections and its parameters.
- [Microcode](exuarch:tab/Microcode): the instruction set. An instruction is a list of steps, and each step names the control lines that are on for one tick, written `device.line`, such as `pc.output`. A step can be limited to certain flags, such as Z=1, which is how a conditional jump is made.
- [Program](exuarch:tab/Program): assembly for the machine. The mnemonics are the instructions from the microcode, followed by their operands. A label ends in `:`, a comment starts with `;`, `.DATA` places values in memory, one cell each, and `.STRING` places text followed by a 0.
- [JSON](exuarch:tab/JSON): the whole machine as one file, kept in step with the editors.
- [Run](exuarch:tab/Run): the machine running. **Tick** (→) runs one clock tick, **Instruction** (Shift+→) finishes the current instruction and fetches the next, **Space** runs and pauses, and **R** resets. Below the machine are the memory, the decoder ROM and a trace of every tick.

## How a tick works

A tick has two halves. First every device whose output line is on drives its value onto its bus; then every device whose load line is on latches the value from its bus. So `pc.output` and `mem.loadmar` in the same step copy the program counter into the memory's address register in one tick.

The decoder runs the microcode. The instruction register counts the steps, and the opcode is where an instruction's steps begin. The fetch routine runs first: it loads the next opcode into the instruction register, and the instruction's own steps follow. The last step of an instruction resets the instruction register, and fetch starts again.

## Build your own

**New…** starts a machine of your own: the minimal CPU, which runs straight away, an empty bus, or a copy of the machine you have open. Your machine lives in the page until you **Download** it, and **Open…** loads the file again. Its note, under *This machine* in this drawer, is yours to write.

A first thing to try, starting from the minimal CPU:

1. In the hardware design, drag a **display** onto the bus and set its ID to `lcd`.
2. In the microcode, add an instruction `OUT` with one operand and two steps: `pc.output` `mem.loadmar`, then `mem.output` `lcd.load` `pc.inc` `ir.reset`.
3. Write the program `OUT 'H'`, `OUT 'i'`, `HLT`, one per line, and run it.

## The examples

The built in machines each take a different direction, as a starting point for your own:

- [BYOC-16](exuarch:package/BYOC-16): a classic accumulator machine on one bus.
- [RISC-16](exuarch:package/RISC-16): load/store with four registers, inspired by ARM.
- [HARVARD-16](exuarch:package/HARVARD-16): three buses, with separate program and data memory.
- [MOVE-16](exuarch:package/MOVE-16): no instruction set, only moves between ports.
- [COPRO-16](exuarch:package/COPRO-16): a CPU that hands work to a blitter on its own bus.
- [IRQ-16](exuarch:package/IRQ-16): interrupts from a timer, the keypad and the blitter.
