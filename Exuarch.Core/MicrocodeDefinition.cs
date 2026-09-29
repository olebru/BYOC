using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
namespace Exuarch.Core
{
    // The microcode of a machine: a fetch routine at opcode 0 followed by the instruction set.
    // Each instruction is a list of micro steps; one step runs per clock tick.
    public class MicrocodeDefinition
    {
        public string Name { get; set; }
        public InstructionDefinition Fetch { get; set; }
        public List<InstructionDefinition> Instructions { get; set; } = new List<InstructionDefinition>();

        // Fetch first, then the instructions, in opcode order.
        [JsonIgnore]
        public IEnumerable<InstructionDefinition> AllInstructions
        {
            get
            {
                if (Fetch != null) yield return Fetch;
                foreach (var instruction in Instructions) yield return instruction;
            }
        }
        public InstructionDefinition FindInstruction(string mnemonic)
        {
            return AllInstructions.FirstOrDefault(i => i.Mnemonic == mnemonic);
        }

        public static MicrocodeDefinition FromJson(string json)
        {
            try
            {
                return JsonSerializer.Deserialize(json, MachineDefinitionJsonContext.Default.MicrocodeDefinition)
                       ?? throw new MachineDefinitionException("Microcode is empty.");
            }
            catch (JsonException e)
            {
                var location = e.Path == null ? "" : $" at {e.Path} (line {e.LineNumber + 1})";
                throw new MachineDefinitionException($"Microcode is not valid JSON{location}: {e.Message}");
            }
        }
        public string ToJson()
        {
            return CompactJson.Format(JsonSerializer.Serialize(this, MachineDefinitionJsonContext.Default.MicrocodeDefinition));
        }
        public MicrocodeDefinition Clone()
        {
            return FromJson(ToJson());
        }
        // Accepts microcode JSON, or the legacy tab separated format.
        public static MicrocodeDefinition Parse(string text)
        {
            return (text ?? "").TrimStart().StartsWith("{") ? FromJson(text) : FromTsv(text ?? "");
        }

        // Converts the legacy tab separated format: clock flag, device, function, mnemonic, N, V, C, Z.
        // A "p" row starts a new micro step, "s" rows add to the current step. Rows of one step must share
        // the same flag condition. The first mnemonic becomes the fetch routine.
        public static MicrocodeDefinition FromTsv(string tsv)
        {
            var microcode = new MicrocodeDefinition();
            var byMnemonic = new Dictionary<string, (InstructionDefinition Instruction, bool StepHasClock)>();
            int lineNumber = 0;
            foreach (var line in SourceText.SplitLines(tsv))
            {
                lineNumber++;
                if (string.IsNullOrWhiteSpace(line)) continue;
                var tokens = line.Split('\t');
                if (tokens.Length < 8)
                {
                    throw new FormatException($"Decoder ROM line {lineNumber}: expected 8 tab separated columns, found {tokens.Length}: '{line}'");
                }
                var clock = tokens[0];
                var status = string.Concat(tokens.Skip(4).Take(4));
                if (status.Length != 4 || status.Any(c => c != '0' && c != '1' && c != 'x'))
                {
                    throw new FormatException($"Decoder ROM line {lineNumber}: status columns must each be 0, 1 or x: '{line}'");
                }
                if (clock != "p" && clock != "s")
                {
                    throw new FormatException($"Decoder ROM line {lineNumber}: clock flag must be 'p' (new step) or 's' (same step): '{line}'");
                }
                var mnemonic = tokens[3];
                if (!byMnemonic.TryGetValue(mnemonic, out var entry))
                {
                    entry = (new InstructionDefinition { Mnemonic = mnemonic }, false);
                    if (microcode.Fetch == null) microcode.Fetch = entry.Instruction;
                    else microcode.Instructions.Add(entry.Instruction);
                }
                var condition = FlagCondition.FromPattern(status);
                var steps = entry.Instruction.Steps;
                bool newStep = steps.Count == 0 || (clock == "p" && entry.StepHasClock);
                if (newStep)
                {
                    steps.Add(new MicroStep { When = condition });
                    entry.StepHasClock = clock == "p";
                }
                else
                {
                    if (!FlagCondition.AreEqual(steps.Last().When, condition))
                    {
                        throw new FormatException($"Decoder ROM line {lineNumber}: flag condition {status} differs from the step it belongs to ({FlagCondition.ToPattern(steps.Last().When)}); start a new step with 'p': '{line}'");
                    }
                    if (clock == "p") entry.StepHasClock = true;
                }
                steps.Last().Signals.Add($"{tokens[1]}.{tokens[2]}");
                byMnemonic[mnemonic] = entry;
            }
            return microcode;
        }
    }

    public class InstructionDefinition
    {
        public string Mnemonic { get; set; }
        public string Description { get; set; }
        // Number of operand cells following the opcode. When set, the assembler checks it.
        public int? Operands { get; set; }
        // What each operand means: a value used as it is, or an address to read, write or jump to. Optional;
        // when given there is one per operand.
        public List<OperandType> OperandTypes { get; set; }
        public List<MicroStep> Steps { get; set; } = new List<MicroStep>();

        // The operand count, from Operands or else from OperandTypes.
        [JsonIgnore]
        public int? OperandCount { get { return Operands ?? OperandTypes?.Count; } }
        // The type of the operand at an index, or null when the instruction does not say.
        public OperandType? OperandTypeAt(int index)
        {
            return OperandTypes != null && index >= 0 && index < OperandTypes.Count ? OperandTypes[index] : null;
        }
        // "LDA address" style usage, or the operand count when the types are not given.
        [JsonIgnore]
        public string Signature
        {
            get
            {
                if (OperandTypes != null && OperandTypes.Count > 0) return Mnemonic + " " + string.Join(", ", OperandTypes.Select(t => t == OperandType.Address ? "address" : "value"));
                return OperandCount switch
                {
                    null => Mnemonic,
                    0 => Mnemonic,
                    1 => Mnemonic + " operand",
                    var n => Mnemonic + " " + string.Join(", ", Enumerable.Range(1, n.Value).Select(i => $"operand{i}")),
                };
            }
        }

        // The steps that run when the status register holds the given flags, in order.
        public List<MicroStep> StepsFor(int status)
        {
            return Steps.Where(s => s.AppliesTo(status)).ToList();
        }
    }

    [JsonConverter(typeof(JsonStringEnumConverter<OperandType>))]
    public enum OperandType
    {
        // Used as it is, for example the number to load.
        [JsonStringEnumMemberName("value")] Value,
        // A memory address to read, write or jump to.
        [JsonStringEnumMemberName("address")] Address,
    }

    public class MicroStep
    {
        // Only run this step when the flags match. Null means always.
        public FlagCondition When { get; set; }
        // Control lines to enable, as "device.line".
        public List<string> Signals { get; set; } = new List<string>();
        public string Comment { get; set; }

        public bool AppliesTo(int status)
        {
            return When == null || When.Matches(status);
        }
    }

    public class FlagCondition
    {
        [JsonPropertyName("N")] public bool? N { get; set; }
        [JsonPropertyName("V")] public bool? V { get; set; }
        [JsonPropertyName("C")] public bool? C { get; set; }
        [JsonPropertyName("Z")] public bool? Z { get; set; }
        // An interrupt request, from the machine's interrupt controller (decoder.interrupts).
        [JsonPropertyName("I")] public bool? I { get; set; }

        // Bit 4 of the decoder status: the four flags are bits 0 to 3.
        public const int InterruptBit = 0x10;

        [JsonIgnore]
        public bool IsAlways { get { return N == null && V == null && C == null && Z == null && I == null; } }

        public bool Matches(int status)
        {
            return Matches(N, status, StatusRegister.NegativeFlag)
                && Matches(V, status, StatusRegister.OverflowFlag)
                && Matches(C, status, StatusRegister.CarryFlag)
                && Matches(Z, status, StatusRegister.ZeroFlag)
                && Matches(I, status, InterruptBit);
        }
        private static bool Matches(bool? required, int status, int flag)
        {
            return required == null || required.Value == ((status & flag) != 0);
        }

        // Pattern of characters for N, V, C, Z and I, each 0, 1 or x. A four character pattern leaves I out.
        // Returns null when every condition is x.
        public static FlagCondition FromPattern(string pattern)
        {
            bool? Flag(int i) => i >= pattern.Length || pattern[i] == 'x' ? null : pattern[i] == '1';
            var condition = new FlagCondition { N = Flag(0), V = Flag(1), C = Flag(2), Z = Flag(3), I = Flag(4) };
            return condition.IsAlways ? null : condition;
        }
        public static string ToPattern(FlagCondition condition)
        {
            char Flag(bool? f) => f == null ? 'x' : f.Value ? '1' : '0';
            return condition == null ? "xxxxx" : $"{Flag(condition.N)}{Flag(condition.V)}{Flag(condition.C)}{Flag(condition.Z)}{Flag(condition.I)}";
        }
        public static bool AreEqual(FlagCondition a, FlagCondition b)
        {
            return ToPattern(a) == ToPattern(b);
        }
        public override string ToString()
        {
            var parts = new List<string>();
            if (N != null) parts.Add($"N={(N.Value ? 1 : 0)}");
            if (V != null) parts.Add($"V={(V.Value ? 1 : 0)}");
            if (C != null) parts.Add($"C={(C.Value ? 1 : 0)}");
            if (Z != null) parts.Add($"Z={(Z.Value ? 1 : 0)}");
            if (I != null) parts.Add($"I={(I.Value ? 1 : 0)}");
            return parts.Count == 0 ? "always" : string.Join(" ", parts);
        }
    }

    // A control line reference, "device.line".
    public readonly struct Signal
    {
        public readonly string Device;
        public readonly string Line;
        public Signal(string device, string line)
        {
            Device = device;
            Line = line;
        }
        public static bool TryParse(string text, out Signal signal)
        {
            signal = default;
            if (text == null) return false;
            var dot = text.IndexOf('.');
            if (dot <= 0 || dot == text.Length - 1 || text.IndexOf('.', dot + 1) >= 0 || text.Any(char.IsWhiteSpace)) return false;
            signal = new Signal(text.Substring(0, dot), text.Substring(dot + 1));
            return true;
        }
        public override string ToString() { return $"{Device}.{Line}"; }
    }
}
