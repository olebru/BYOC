# ExµArch

Explore microarchitectures: a computer architecture playground in the browser. Design a machine from devices and buses, write the microcode that drives it and the instruction set that microcode defines, then watch programs run on it one clock tick at a time.

Live at **[exuarch.com](https://exuarch.com)**.

## What you can do

- **Hardware design:** place registers, an ALU, memory, an MMU, displays, a keypad, timers, an interrupt controller, a blitter, a triangle rasterizer and more on any number of buses, and wire them together. The decoder sits on the canvas too.
- **Microcode:** write each instruction as steps of control lines (`device.line`), with steps that run only for certain flags.
- **Programs:** write assembly for the instruction set you made, with completion, hover help and formatting.
- **Run:** step tick by tick or run at full speed, with the buses, every device, memory, the decoder ROM and a trace on screen.
- **Seven example machines**, from a classic accumulator CPU (BYOC-16) to a load/store machine, a Harvard machine, a transport triggered one, a CPU with a coprocessor, one with interrupts and a small 3D GPU.
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

Every push to `main` is built, tested and deployed to Azure Static Web Apps by `.github/workflows/deploy.yml`. Each pull request gets a preview site of its own, linked from the pull request and removed when it closes.
