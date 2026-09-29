using System.Collections.Generic;
using System.Linq;
namespace Exuarch.Core
{
    // Starting points for a new machine, as packages with no example programs.
    public static class MachineTemplates
    {
        public const string StarterProgram = "; A new machine: write your program here.\n        NOP\n        HLT";

        // The least that runs: a bus, a program counter, program memory, the instruction register, a status register
        // and a clock, with a fetch routine and NOP, JMP and HLT to build the instruction set from.
        public static MachinePackage Minimal(string name)
        {
            MicroStep Step(params string[] signals) => new MicroStep { Signals = signals.ToList() };
            var machine = new MachineDefinition
            {
                Name = name,
                Buses = { new BusDefinition { Id = "main" } },
                Devices =
                {
                    new DeviceDefinition { Id = "pc", Type = "register", Name = "PC", Bus = "main" },
                    new DeviceDefinition { Id = "ir", Type = "instructionRegister", Name = "IR", Bus = "main" },
                    new DeviceDefinition { Id = "mem", Type = "ram", Name = "MEMORY", Bus = "main" },
                    new DeviceDefinition { Id = "status", Type = "statusRegister", Name = "STATUS", Bus = "main" },
                    new DeviceDefinition { Id = "clk", Type = "clock", Name = "CLOCK" },
                },
                Decoder = new DecoderDefinition
                {
                    Status = "status",
                    InstructionRegister = "ir",
                    Microcode = new MicrocodeDefinition
                    {
                        Name = $"{name} microcode",
                        Fetch = new InstructionDefinition
                        {
                            Mnemonic = "FETCH",
                            Description = "Load the opcode at the program counter into the instruction register and step past it",
                            Steps = { Step("pc.output", "mem.loadmar"), Step("mem.output", "ir.load", "pc.inc") },
                        },
                        Instructions = new List<InstructionDefinition>
                        {
                            new InstructionDefinition { Mnemonic = "NOP", Description = "Do nothing", Operands = 0, Steps = { Step("ir.reset") } },
                            new InstructionDefinition
                            {
                                Mnemonic = "JMP", Description = "Jump to the address", Operands = 1, OperandTypes = new List<OperandType> { OperandType.Address },
                                Steps = { Step("pc.output", "mem.loadmar"), Step("mem.output", "pc.load", "ir.reset") },
                            },
                            new InstructionDefinition { Mnemonic = "HLT", Description = "Halt the machine", Operands = 0, Steps = { Step("clk.disable") } },
                        },
                    },
                },
                Halt = "clk",
                ProgramMemory = "mem",
            };
            machine.EnsureLayout();
            return new MachinePackage { Name = name, Description = "A new machine, started from the minimal CPU", Machine = machine };
        }

        // One bus and nothing else: devices, decoder and microcode are all up to you.
        public static MachinePackage Empty(string name)
        {
            var machine = new MachineDefinition { Name = name, Buses = { new BusDefinition { Id = "main" } } };
            machine.EnsureLayout();
            return new MachinePackage { Name = name, Description = "A new, empty machine", Machine = machine };
        }

        // The given machine under a new name, with its programs.
        public static MachinePackage CopyOf(MachinePackage package, string name)
        {
            var copy = package.Clone();
            copy.Name = name;
            copy.Description = $"A copy of {package.Name}";
            copy.Machine.Name = name;
            return copy;
        }
    }
}
