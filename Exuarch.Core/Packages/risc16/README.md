# RISC-16

A small **load/store** machine in the spirit of ARM, kept deliberately simple. Where BYOC-16 lets many instructions touch memory, RISC-16 separates the two worlds: arithmetic only ever happens in registers, and memory is only reached by loads and stores (and by pushes and pops on the stack).

## The idea

RISC designs trade a rich instruction set for a regular one. Every ALU instruction has the same shape, memory is reached by loads and stores (plus the stack), and subroutine calls use a **link register** instead of the stack. That regularity is what made RISC processors easy to pipeline and fast.

To keep the hardware plain, the registers here are explicit: [R0](exuarch:device/r0), [R1](exuarch:device/r1), [R2](exuarch:device/r2) and [R3](exuarch:device/r3) are four ordinary register devices, and the register an instruction uses is part of its name, as in `MOV_R1_R0` or `ADD_R2`. That is how early 8 bit CPUs worked too, and it means there is no register file to decode.

## The parts

- [R0](exuarch:device/r0) is the accumulator. It is wired to [the ALU](exuarch:device/alu)'s first input, and every ALU result, load, `OUT` and `PLOT` goes through it.
- [The operand latch](exuarch:device/opb) holds the ALU's second input for one instruction.
- [CPSR](exuarch:device/cpsr), named after ARM's status register, holds the N, V, C and Z flags.
- [SP](exuarch:device/sp) runs a full descending stack in [memory](exuarch:device/mem), and [LR](exuarch:device/lr) keeps the return address of a `BL` call.

## How an instruction runs

[ADD_R2](exuarch:instruction/ADD_R2) is two steps after fetch: put R2 in the operand latch, then let the ALU drive R0 + R2 back into R0 and set the flags.

Memory is reached through an address register. [LDR_R1](exuarch:instruction/LDR_R1) loads R0 from the address held in R1, and [STR_R1](exuarch:instruction/STR_R1) stores R0 there. To walk a string you point R1 at it with [ADR_R1](exuarch:instruction/ADR_R1) and step it with [INC_R1](exuarch:instruction/INC_R1), which counts without touching the flags.

[BL](exuarch:instruction/BL) calls a subroutine by loading the PC and saving the address after the call in LR; [RET](exuarch:instruction/RET) moves LR back into the PC. No memory is touched, which is why RISC calls are cheap.

## Things to try

1. [Hello, world on the LCD](<exuarch:program/Hello, world on the LCD>) prints through a subroutine called with `BL`.
2. [Fibonacci on the LCD](<exuarch:program/Fibonacci on the LCD>) stops exactly when an `ADD` carries out of 16 bits, and saves R1 to R3 on the stack inside its print routine.
3. [Sketch with the arrow keys](<exuarch:program/Sketch with the arrow keys>) reads the keypad with `IN`. In the [Run view](exuarch:tab/Run), switch on ⚡ Max, click the keypad's *Use the keyboard* and draw.
4. Count the ticks: with fetch, an `ADD_R2` takes 4, and so does a `LDR_R1`. BYOC-16's `LDA`, which reads its address from the program and reaches into memory itself, takes 6.

## Read more

- [Flags and conditions](exuarch:guide/flags-and-conditions): the flags the branches test.
- [Subroutines and the stack](exuarch:guide/subroutines-and-the-stack): calls and returns through a stack, to compare with the link register.
- [Operands](exuarch:guide/operands): value and address operands.
