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
        public const int OpCodeAddressSpace = 256;
        public const int StatusVariants = 16;
        private List<MicroInstruction> completeROM;
        private Dictionary<int, List<MicroInstruction>> romByOpCode;
        private Dictionary<string, int> baseAddressByMnemonic;
        private int opCodesUsed;

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
                            int opCode = (status << 8) | (addr + step);
                            completeROM.Add(new MicroInstruction(opCode, signal.Device, signal.Line, instruction.Mnemonic, false, step == 0 && status == 0));
                        }
                    }
                }
                addr += steps;
            }
            opCodesUsed = addr;
            if (addr > OpCodeAddressSpace)
            {
                throw new Exception($"OpCode AddressSpace is exhausted, {addr} opcodes needed but only {OpCodeAddressSpace} available, optimize...");
            }
            romByOpCode = completeROM.GroupBy(m => m.OPCode).ToDictionary(g => g.Key, g => g.ToList());
        }

        public MicrocodeDefinition Microcode { get; }
        public IReadOnlyList<MicroInstruction> MicroInstructions { get { return completeROM; } }
        public int OpCodesUsed { get { return opCodesUsed; } }

        public byte FetchByteCodeFromMnemonic(string Mnemonic)
        {
            if (Mnemonic == null || !baseAddressByMnemonic.TryGetValue(Mnemonic, out var baseAddress))
            {
                throw new ArgumentException($"Unknown mnemonic '{Mnemonic}', it is not defined in the decoder ROM.");
            }
            return (byte)baseAddress;
        }
        // Operand bytes the instruction declares, or null when it does not say.
        public int? OperandCount(string mnemonic)
        {
            return Microcode.FindInstruction(mnemonic)?.Operands;
        }
        // The decoder only has 4 status inputs (NVCZ), so higher status bits are ignored.
        public List<MicroInstruction> FetchInstruction(Byte StatusRegisterValue, Byte InstructionRegisterValue)
        {
            int fullOpCode = ((StatusRegisterValue & 0x0F) << 8) | InstructionRegisterValue;
            return romByOpCode.TryGetValue(fullOpCode, out var microInstructions)
                ? new List<MicroInstruction>(microInstructions)
                : new List<MicroInstruction>();
        }
        public double OpCodeAddressSpaceUsedInPercent()
        {
            return Math.Round(((double)opCodesUsed / OpCodeAddressSpace * 100d), 1);
        }
    }
}
