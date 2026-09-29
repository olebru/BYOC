using System;
using System.Collections.Generic;
using System.Linq;
namespace BYOCCore
{
    // The compiled microcode. An address is the 4 status bits (NVCZ) followed by the 8 bit micro step
    // address held in the instruction register. Each instruction gets a block of consecutive step
    // addresses, sized for its longest flag variant.
    public class DecoderRom
    {
        // The micro step register is 16 bits, so there are 2^16 step addresses per flag value.
        public const int StepBits = 16;
        public const int AddressSpace = 1 << StepBits;
        // The four flags and the interrupt request: bits 0 to 4 of the decoder status.
        public const int StatusVariants = 32;
        public const int StatusMask = StatusVariants - 1;
        private List<MicroInstruction> completeROM;
        private Dictionary<int, List<MicroInstruction>> romByOpCode;
        private Dictionary<string, int> baseAddressByMnemonic;
        private int opCodesUsed;
        private readonly List<(InstructionDefinition Instruction, int Base, int Count)> ranges = new List<(InstructionDefinition, int, int)>();
        // The steps each instruction runs for each of the 16 status values, parallel to ranges.
        private readonly List<List<MicroStep>[]> variants = new List<List<MicroStep>[]>();

        // Accepts microcode JSON, or the legacy tab separated format.
        public DecoderRom(string microcode) : this(MicrocodeDefinition.Parse(microcode))
        {
        }

        public DecoderRom(MicrocodeDefinition microcode)
        {
            Microcode = microcode;
            completeROM = new List<MicroInstruction>();
            baseAddressByMnemonic = new Dictionary<string, int>();
            int addr = 0;
            foreach (var instruction in microcode.AllInstructions)
            {
                if (baseAddressByMnemonic.ContainsKey(instruction.Mnemonic ?? ""))
                {
                    throw new ArgumentException($"Mnemonic '{instruction.Mnemonic}' is defined more than once.");
                }
                baseAddressByMnemonic[instruction.Mnemonic ?? ""] = addr;
                int steps = 1;
                for (int status = 0; status < StatusVariants; status++)
                {
                    var variant = instruction.StepsFor(status);
                    steps = Math.Max(steps, variant.Count);
                    for (int step = 0; step < variant.Count; step++)
                    {
                        foreach (var text in variant[step].Signals)
                        {
                            if (!Signal.TryParse(text, out var signal))
                            {
                                throw new FormatException($"{instruction.Mnemonic} step {step}: '{text}' is not a signal, write it as device.line");
                            }
                            int opCode = (status << StepBits) | (addr + step);
                            completeROM.Add(new MicroInstruction(opCode, signal.Device, signal.Line, instruction.Mnemonic, false, step == 0 && status == 0));
                        }
                    }
                }
                ranges.Add((instruction, addr, steps));
                variants.Add(Enumerable.Range(0, StatusVariants).Select(s => instruction.StepsFor(s)).ToArray());
                addr += steps;
            }
            opCodesUsed = addr;
            if (addr > AddressSpace)
            {
                throw new Exception($"OpCode AddressSpace is exhausted, {addr} opcodes needed but only {AddressSpace} available, optimize...");
            }
            romByOpCode = completeROM.GroupBy(m => m.OPCode).ToDictionary(g => g.Key, g => g.ToList());
        }

        public MicrocodeDefinition Microcode { get; }
        // Full ROM address for a decoder status and micro step: (status & 0x1F) << StepBits | step.
        public static int RomAddress(int status, int step) { return ((status & StatusMask) << StepBits) | (step & (AddressSpace - 1)); }
        // Each instruction's block of micro step addresses, in address order.
        public IReadOnlyList<(InstructionDefinition Instruction, int Base, int Count)> Blocks { get { return ranges; } }
        public IReadOnlyList<MicroInstruction> MicroInstructions { get { return completeROM; } }
        public int OpCodesUsed { get { return opCodesUsed; } }

        // The instruction whose micro step block contains the address, and the step that runs there for
        // the given status flags (null when that flag variant has no step at this offset).
        public (InstructionDefinition Instruction, MicroStep Step, int Offset)? Locate(int statusRegisterValue, int instructionRegisterValue)
        {
            for (int i = 0; i < ranges.Count; i++)
            {
                var range = ranges[i];
                if (instructionRegisterValue < range.Base || instructionRegisterValue >= range.Base + range.Count) continue;
                int offset = instructionRegisterValue - range.Base;
                var variant = variants[i][statusRegisterValue & StatusMask];
                return (range.Instruction, offset < variant.Count ? variant[offset] : null, offset);
            }
            return null;
        }
        public int FetchByteCodeFromMnemonic(string Mnemonic)
        {
            if (Mnemonic == null || !baseAddressByMnemonic.TryGetValue(Mnemonic, out var baseAddress))
            {
                throw new ArgumentException($"Unknown mnemonic '{Mnemonic}', it is not defined in the decoder ROM.");
            }
            return baseAddress;
        }
        // Operand bytes the instruction declares, or null when it does not say.
        public int? OperandCount(string mnemonic)
        {
            return Microcode.FindInstruction(mnemonic)?.OperandCount;
        }
        // The decoder only has 4 status inputs (NVCZ), so higher status bits are ignored.
        public List<MicroInstruction> FetchInstruction(int StatusRegisterValue, int InstructionRegisterValue)
        {
            int fullOpCode = RomAddress(StatusRegisterValue, InstructionRegisterValue);
            return romByOpCode.TryGetValue(fullOpCode, out var microInstructions)
                ? new List<MicroInstruction>(microInstructions)
                : new List<MicroInstruction>();
        }
        public double OpCodeAddressSpaceUsedInPercent()
        {
            return Math.Round(((double)opCodesUsed / AddressSpace * 100d), 1);
        }
    }
}
