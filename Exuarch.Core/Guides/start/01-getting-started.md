# Getting started

ExµArch is a workbench for computers that don't exist yet. You wire up the hardware, write the microcode that makes it do things, write a program in the instruction set that microcode defines, and watch it run one clock tick at a time. Nothing is fixed: the number of buses, the registers, the instruction set, even whether there is an instruction set at all.

This handbook has three parts: tutorials that build a machine step by step, concept pages that explain one idea each, and a reference page for every kind of device. Search at the top of the contents covers all of them and the machine you have open. The small **?** buttons around the app open the page about whatever they sit next to.

## Build a machine, step by step

Each tutorial starts where the last one stopped, beginning from **New…** and the minimal CPU. Every step is checked by a test, so what you build will run.

1. [Your first machine](exuarch:guide/first-machine): add a display and an instruction that writes to it, then print "Hi".
2. [Registers and the ALU](exuarch:guide/registers-and-the-alu): two registers and an ALU, and a program that computes the characters it prints.
3. [Loops and flags](exuarch:guide/loops-and-flags): a conditional jump built from steps that only run for one value of a flag, and a countdown.
4. [Subroutines and the stack](exuarch:guide/subroutines-and-the-stack): a stack pointer, `CALL` and `RET`.
5. [Reading the keypad](exuarch:guide/reading-the-keypad): input, and a program that echoes the keys you press.

## The tabs

- [Hardware design](<exuarch:tab/Hardware design>) holds the devices and the buses between them. Drag a device from the palette onto the canvas and it lands on the nearest bus; select it to see its control lines, connections and parameters.
- [Microcode](exuarch:tab/Microcode) holds the instruction set: each instruction is a list of steps, and each step names the control lines that are on for one tick.
- [Program](exuarch:tab/Program) is assembly for the machine, using the mnemonics the microcode defines. **＋ New program** starts an empty one.
- [JSON](exuarch:tab/JSON) is the whole machine as one file, kept in step with the editors.
- [Run](exuarch:tab/Run) runs it: **Tick** (→) runs one clock tick, **Instruction** (Shift+→) finishes the current instruction and fetches the next, **Space** runs and pauses, and **R** resets. Below the machine are the memory, the decoder ROM and a trace of every tick.

## How the pieces fit

A machine is devices on [buses](exuarch:guide/buses-and-ticks). Every tick has two halves: devices whose output line is on drive their bus, then devices whose load line is on take the value. What a device can do is its set of [control lines](exuarch:guide/devices-and-control-lines).

The [microcode](exuarch:guide/microcode) decides which lines are on in each tick. A [fetch routine](exuarch:guide/fetch-and-the-instruction-register) loads the next opcode, and the instruction register steps through that instruction's microcode. [Flags and conditions](exuarch:guide/flags-and-conditions) let a step run only when a flag has a certain value, which is how a machine makes decisions. An instruction's [operands](exuarch:guide/operands) are the cells that follow its opcode.

Programs are written in [assembly](exuarch:guide/assembly): mnemonics, labels, `.DATA` and `.STRING`. They are loaded into [memory](exuarch:guide/memory-and-banks), which can also be split into banks.

Beyond one bus and one CPU: [bridges](exuarch:guide/bridges) move values between buses, [bus masters](exuarch:guide/bus-masters) such as the blitter work on a bus of their own, and [interrupts](exuarch:guide/interrupts) let devices stop the program to be served. A whole machine, with its note and programs, is saved as a [package](exuarch:guide/packages). And [the graphics pipeline](exuarch:guide/graphics-pipeline) puts several of these together into a small 3D GPU.

## The examples

The built in machines each take a different direction. Load one from **Examples** in this drawer, take it apart, and start your own from a copy with **New…**.

- [BYOC-16](exuarch:package/BYOC-16): a classic accumulator machine on one bus.
- [RISC-16](exuarch:package/RISC-16): load/store with four registers, inspired by ARM.
- [HARVARD-16](exuarch:package/HARVARD-16): three buses, with separate program and data memory.
- [MOVE-16](exuarch:package/MOVE-16): no instruction set, only moves between ports.
- [COPRO-16](exuarch:package/COPRO-16): a CPU that hands work to a blitter on its own bus.
- [GPU-16](exuarch:package/GPU-16): a small 3D pipeline, with shaded triangles, a depth buffer and a spinning cube.
- [IRQ-16](exuarch:package/IRQ-16): interrupts from a timer, the keypad and the blitter.
