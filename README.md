# ExµArch

Explore microarchitectures: a computer architecture playground in the browser. Design a machine from devices and buses, write the microcode that drives it and the instruction set that microcode defines, then watch programs run on it one clock tick at a time.

## Use it online

ExµArch runs at **[www.exuarch.com](https://www.exuarch.com)**. There is nothing to install and no account: open the page and start with the getting started drawer, which leads to the tutorials, the handbook and the example machines.

- It runs entirely in your browser. Machines, microcode and programs never leave it; they are saved in the browser's own storage, so they are there next time on the same browser and device.
- To move a machine to another browser or share it, use **Export** to save it as a file and **Import** to open it.
- After the first visit the site works offline, and it can be installed as an app from the browser's menu.
- The site is built from `main`, so it always runs the latest merged version.

## What you can do

- **Hardware design:** place registers, an ALU, memory, an MMU, displays, a keypad, timers, an interrupt controller, a blitter, a triangle rasterizer and more on any number of buses, and wire them together. The decoder sits on the canvas too.
- **Microcode:** write each instruction as steps of control lines (`device.line`), with steps that run only for certain flags.
- **Programs:** write assembly for the instruction set you made, with completion, hover help and formatting.
- **Run:** step tick by tick or run at full speed, with the buses, every device, memory, the decoder ROM and a trace on screen.
- **Ten example machines**, from TINY-16 with one register and a classic accumulator CPU (BYOC-16) to a load/store machine, one with a register file, a Harvard machine, a transport triggered one, a CPU with a coprocessor, one with interrupts and a small 3D GPU, with and without a double buffered screen.
- **A handbook** in the app: tutorials that build a machine step by step, concept pages and a reference page for every device type.
- **Your work is kept in the browser.** Export saves a machine as a file and Import opens one.

## Building and running

You need the .NET 10 SDK (see `global.json`).

```sh
dotnet run --project Exuarch.Web     # the app, on the URL it prints
dotnet test Exuarch.Core.Tests       # the tests
```

## The code

- `Exuarch.Core`: the simulator. Devices and their metadata (`DeviceRegistry.cs`), the machine and its two-phase clock (`Machine.cs`, `Clocking.cs`), the decoder ROM, the microcode validator, the assembler and editor support, the built in packages (`Packages/`) and the handbook pages (`Guides/`).
- `Exuarch.Web`: the Blazor WebAssembly app.
- `Exuarch.Core.Tests`: the tests, including checks that every handbook link leads somewhere, that each tutorial's machine is built and runs as the text says, and that every example program assembles and runs.

## Deployment

Every push to `main` is built, tested and deployed to Azure Static Web Apps, which serves [www.exuarch.com](https://www.exuarch.com), by `.github/workflows/deploy.yml`. Each pull request gets a preview site of its own, linked from the pull request and removed when it closes.
