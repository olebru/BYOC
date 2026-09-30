# Operands

Most instructions need something to work on: a number to load, an address to read, a place to jump to. That something is an operand, stored in memory right after the opcode. The microcode decides how an instruction reads its operands; the instruction's definition tells the assembler and the editor how many there are and what they mean, so they can check your program and explain it.

## Operands in memory

Every opcode and every operand takes one 16 bit cell. `LAI 42` assembles to two cells, the opcode of `LAI` and then 42; an instruction with two operands takes three cells. Nothing in the hardware knows this. It is the microcode that steps the program counter over the operand cells and reads them, and the definition that tells the assembler to expect them.

## Count and types

Each instruction in the [Microcode](exuarch:tab/Microcode) tab has an **Operands** count and, when the count is above 0, an **Operand types** choice per operand:

- **value**: used as it is, like the number `LAI` loads.
- **address**: a memory location to read, write or jump to, like the target of `JMP`.

In the [JSON](exuarch:tab/JSON) these are `operands` and `operandTypes`:

```json
{ "mnemonic": "LDA", "operands": 1, "operandTypes": ["address"], "steps": [ ... ] }
```

Both are optional. Without `operands` the count is taken from the number of `operandTypes`; with neither, the assembler does not check how many operands a line has. When both are given they must agree, or the microcode editor reports an error.

The types never change what the assembler produces. They change what it checks and what the editor tells you: hovering over `LDA` in the program editor shows `LDA address`, and hovering over an operand says whether it is used as an address or as a value.

## What the assembler accepts

Operands follow the mnemonic, separated by commas. Each one is one of:

- a number, `42` or `0x2A`, from 0 to 65535;
- a character, `'A'`, which is its Latin-1 code;
- a label, which is the address the label marks.

A leading `#`, as in `#42`, is accepted and means the same. A string is not an operand; strings only go in `.DATA` and `.STRING` lines.

The assembler reports an error when a line has a different number of operands than the instruction declares, when a number is out of range, and when a label is not defined.

## Labels as values

A label is always an address. That is exactly right for an address operand, `JMP loop`, but usually a mistake for a value operand: `LAI msg` loads the address of `msg`, not what is stored there. So when an instruction declares a value operand and you give it a label, the assembler warns:

```
LAI takes a value here, and 'msg' is the address of a label (0x0012). Use a number, or an instruction that takes an address.
```

It is a warning, not an error, because sometimes the address is what you want, for example to load a pointer into a register. [RISC-16](exuarch:package/RISC-16)'s `ADR_R1` takes an address operand for exactly that reason. The warning only appears when the instruction declares its operand types.

## Reading operands in microcode

An instruction reads an operand like fetch reads an opcode: the program counter goes on the bus as an address, the cell comes back on the bus, and the program counter moves on. [BYOC-16](exuarch:package/BYOC-16)'s fetch leaves the program counter on the opcode, so its `LAI` starts by stepping onto the operand:

```
step 1: pc.inc
step 2: pc.output, mem.loadmar
step 3: mem.output, rega.load, regi.reset, pc.inc
```

For a value operand the cell itself is the result, loaded straight into A in step 3.

For an address operand the cell is used as an address, which costs another tick. `LDA` reads the operand into the data memory's address register and only then reads the data:

```
step 1: pc.inc
step 2: pc.output, mem.loadmar
step 3: mem.output, mmu.loadmar
step 4: rega.load, mmu.output, regi.reset, pc.inc
```

An instruction with several operands repeats the read for each one, stepping the program counter after every cell, and must leave the program counter on the next opcode before it returns to fetch. If an instruction reads fewer operand cells than it declares, the rest will be fetched as if they were opcodes.

## See also

- [Fetch and the instruction register](exuarch:guide/fetch-and-the-instruction-register)
- [Assembly](exuarch:guide/assembly)
- [Microcode](exuarch:guide/microcode)
- [Memory and banks](exuarch:guide/memory-and-banks)
