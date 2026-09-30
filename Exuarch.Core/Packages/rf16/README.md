# RF-16

A load/store machine like [RISC-16](exuarch:package/RISC-16), with one difference that changes the whole instruction set: its eight registers live in a **register file**, and the register an instruction uses is an operand. RISC-16 writes `MOV_R1_R0` and `ADD_R2`; RF-16 writes `MOV R1, R0` and `ADD R0, R2`, and every register works with every instruction.

## The idea

When registers are separate devices, the microcode names them, so each register needs its own opcode: four registers already give RISC-16 sixteen `MOV_Rx_Ry` instructions, and only R0 can be added to. A register file turns the register into a number. The program writes `R5`, the assembler puts 5 in the operand cell, and the microcode moves that cell into the register file's select latch like any other value. Eight registers, or sixteen, cost no more opcodes than one.

This is how most real processors work, except that they pack the register numbers into the instruction word itself. Here each register number takes a cell of its own after the opcode, like every other operand.

## The parts

- [The register file](exuarch:device/rf) holds R0 to R7. `select` latches a register number from the bus, and `output`, `load`, `inc` and `dec` then work on the selected register.
- [DESTINATION](exuarch:device/d) remembers the first register number while the instruction selects the second one.
- [ALU A](exuarch:device/x) and [ALU B](exuarch:device/y) hold the ALU's two inputs, since neither can be wired to one fixed register any more.
- [FLAGS](exuarch:device/flags) holds the N, V, C and Z flags; [SP](exuarch:device/sp) runs a full descending stack in [memory](exuarch:device/mem).

## How an instruction runs

[ADD](exuarch:instruction/ADD) `Rd, Rs` reads the first operand into DESTINATION and the second into the select latch. It copies Rs into ALU B, selects Rd again from DESTINATION, copies Rd into ALU A, and lets the ALU drive the sum back into Rd. Only one register can be on the bus at a time, so every register it touches is a step of its own.

[LDR](exuarch:instruction/LDR) `Rd, Ra` and [STR](exuarch:instruction/STR) `Rs, Ra` reach memory through any register, and [INC](exuarch:instruction/INC) steps any register without touching the flags. [ADR](exuarch:instruction/ADR) takes a register and an address, so `ADR R1, msg` is the way to point a register at a label.

In the program editor, hover over a register operand to see its number, and type a comma after a mnemonic to get the registers offered. Writing a label where a register belongs, or `R8` on an eight register machine, is an error.

## Things to try

1. [Hello, world on the LCD](<exuarch:program/Hello, world on the LCD>) walks the string with `LDR R0, R1` and `INC R1`.
2. [Fibonacci on the LCD](<exuarch:program/Fibonacci on the LCD>) is RISC-16's program with register operands. With eight registers, the print routine keeps to R4 to R7 and saves nothing on the stack.
3. Count the ticks: with fetch, `ADD R0, R2` takes 10, where RISC-16's `ADD_R2` takes 4. Four of them read the two operand cells, the rest move registers one at a time over the single bus. Real register files have two read ports for this reason.
4. Set the register file's **count** to 16 and write `R15`.

## Read more

- [Operands](exuarch:guide/operands): value, address and register operands.
- [Registers and the ALU](exuarch:guide/registers-and-the-alu): the ALU's inputs and flags.
- [Flags and conditions](exuarch:guide/flags-and-conditions): the flags the branches test.
