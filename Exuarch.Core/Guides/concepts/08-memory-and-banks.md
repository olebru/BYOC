# Memory and banks

Registers hold a handful of values; memory holds the program, its data and the stack. In ExµArch a memory is a device like any other: it sits on a bus, has its own address register, and does nothing until a control line in a microcode step tells it to. Knowing how that address register behaves from tick to tick is most of what it takes to write load and store instructions.

## Cells and addresses

Every memory cell holds one 16 bit word, like every bus and register in the machine. Addresses count cells, not bytes: address 5 is the sixth word. A cell that has never been written reads as 0.

## The ram device

A [`ram`](exuarch:reference/ram) device has a **size** parameter, the number of cells, from 1 to 65536 (4096 if you leave it out), and four control lines:

- `loadmar` takes an address from the bus into the memory address register, the MAR. The address wraps at the size, so in a 4096 cell memory, 4100 addresses cell 4.
- `output` puts the cell at the MAR on the bus.
- `load` stores the bus value in the cell at the MAR.
- `outputmar` puts the MAR itself on the bus.

The MAR belongs to the memory. Each memory keeps its own address until the next `loadmar`, so two memories on the same bus never get in each other's way.

## Timing: address first, then data

A tick has two halves: devices drive the buses, then devices latch (see [buses and ticks](exuarch:guide/buses-and-ticks)). `output` drives the cell at the address the MAR held when the tick began. `load` stores into the address the MAR held when the tick began. The new address from `loadmar` only arrives at the end of the tick.

So a memory access takes two steps: set the address, then read or write. The fetch routine of the minimal CPU does exactly that:

```json
{ "signals": ["pc.output", "mem.loadmar"] },
{ "signals": ["mem.output", "ir.load", "pc.inc"] }
```

If you put `loadmar` and `load` in the same step, the value goes to the old address.

## Program memory

The machine's `programMemory` names the memory the assembled program is loaded into, starting at address 0. It must be a `ram` device, and the program must fit in it. The same memory can hold data too: a `.DATA` line in the program is just more cells in it. See [assembly](exuarch:guide/assembly).

A machine can have as many memories as you like. HARVARD-16 keeps its program in one memory on the instruction bus and its data in another on the data bus, so a fetch and a data access can happen in the same tick.

## Banks: the mmu device

An [`mmu`](exuarch:reference/mmu) is several ram banks behind one chip select register. Its parameters are **banks**, from 1 to 256 (16 if you leave it out), and **bankSize**, the cells per bank (4096 by default).

The four memory lines, `loadmar`, `output`, `load` and `outputmar`, work on the bank the chip select register points at. Three more lines manage that register:

- `loadcs` takes a bank number from the bus. Numbers wrap at the number of banks.
- `outputcs` puts the selected bank number on the bus.
- `select0stack` selects bank 0.

Each bank has its own MAR. Switching banks does not move an address from one bank to the other.

Within a tick, `select0stack` applies first. `output` and `outputmar` use the bank that was selected at the start of the tick, and `loadmar` and `load` use the bank selected after `loadcs` has latched. So `loadcs` and `loadmar` in the same step address the new bank.

BYOC-16 uses an mmu for data and its stack. Its push instruction, `PSA`, saves the selected bank, switches to bank 0 with `select0stack`, stores A at the stack pointer, and then selects the saved bank again. That way a program can work in any bank and still share one stack in bank 0.

An mmu can not be the program memory, which has to be a plain `ram`.

## Watching memory in Run

The [Run](exuarch:tab/Run) tab's **Memory** panel shows every ram and mmu in the machine, with a button for each. Rows show 16 cells in hexadecimal and an ASCII column with the low 8 bits of each cell.

Cells are marked for the program counter, the MAR, the instruction that is running and the cells just written. For an mmu you choose the bank, and **selected** jumps to the one the chip select register points at. Large memories are split into pages of 256 cells, and **go to MAR** turns to the page the address register is in. Each tick's memory writes also appear in the **Last tick** panel, with the bank for an mmu.

## See also

- [Buses and the two-phase tick](exuarch:guide/buses-and-ticks)
- [Subroutines and the stack](exuarch:guide/subroutines-and-the-stack)
- [ram reference](exuarch:reference/ram) and [mmu reference](exuarch:reference/mmu)
- [BYOC-16](exuarch:package/BYOC-16) and [HARVARD-16](exuarch:package/HARVARD-16)
